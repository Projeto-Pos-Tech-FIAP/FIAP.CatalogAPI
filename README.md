# FIAP.CatalogAPI

Microserviço de catálogo de jogos da plataforma FIAP Games. Responsável pelo CRUD de jogos, pela busca avançada em Elasticsearch e pelo fluxo de compra via Apache Kafka, com autenticação via Keycloak.

---

## Índice

1. [Arquitetura](#1-arquitetura)
2. [Estrutura do Projeto](#2-estrutura-do-projeto)
3. [Fluxo de Compra](#3-fluxo-de-compra)
4. [Autenticação com Keycloak](#4-autenticação-com-keycloak)
5. [Requisitos de Orquestração — Validação](#5-requisitos-de-orquestração--validação)
6. [Manifestos Kubernetes — CatalogAPI](#6-manifestos-kubernetes--catalogapi)
7. [Deploy — Passo a Passo Completo](#7-deploy--passo-a-passo-completo)
8. [Executar Localmente com Docker Compose](#8-executar-localmente-com-docker-compose)
9. [Endpoints da API](#9-endpoints-da-api)
10. [Persistência Poliglota — MongoDB e Redis](#10-persistência-poliglota--mongodb-e-redis)
11. [Busca Avançada — Elasticsearch](#11-busca-avançada--elasticsearch)

---

## 1. Arquitetura

O projeto segue **Clean Architecture** com quatro camadas bem definidas:

```
┌──────────────────────────────────────────────────────────┐
│                    CatalogAPI.Api                        │
│   GameController · PurchaseController · AuthController   │
│   ExceptionMiddleware · Program.cs                       │
└─────────────────────────┬────────────────────────────────┘
                          │
┌─────────────────────────▼────────────────────────────────┐
│                CatalogAPI.Application                    │
│   IGameService · IPurchaseService · IKeycloakService     │
│   GameService  · PurchaseService  · DTOs · AutoMapper    │
└──────────────┬──────────────────────────────┬────────────┘
               │                              │
┌──────────────▼──────────┐   ┌───────────────▼────────────┐
│   CatalogAPI.Domain     │   │  CatalogAPI.Infrastructure  │
│   Entities · Events     │   │  EF Core · Repositories     │
│   Interfaces · Exceptions│  │  MongoDB Audit · Redis      │
└─────────────────────────┘   │  Kafka · Keycloak · BgSvc   │
                              └─────────────────────────────┘
```

### Stack

| Tecnologia | Versão | Uso |
|---|---|---|
| .NET / ASP.NET Core | 10.0 | Runtime |
| Entity Framework Core | 10.0 | ORM — SQL Server |
| AutoMapper | 12.0.1 | Mapeamento Entity ↔ DTO |
| Confluent.Kafka | 2.x | Producer/Consumer Kafka |
| MongoDB.Driver | 3.x | Audit logs |
| Microsoft.Extensions.Caching.StackExchangeRedis | 10.0.9 | Cache distribuído (`IDistributedCache`) |
| Elastic.Clients.Elasticsearch | 8.19 | Busca avançada (fuzzy + relevância) |
| Keycloak | 24.0 | Autenticação JWT |
| Docker | — | Containerização |
| Kubernetes | 1.28+ | Orquestração |

---

## 2. Estrutura do Projeto

```
FIAP.CatalogAPI/
├── src/
│   ├── FIAP.CatalogAPI.Domain/          # Entidades, interfaces, eventos, exceções
│   ├── FIAP.CatalogAPI.Application/     # Serviços, DTOs, mapeamentos
│   ├── FIAP.CatalogAPI.Infrastructure/  # EF Core, Kafka, MongoDB, Redis, Keycloak
│   ├── FIAP.CatalogAPI.Api/             # Controllers, middlewares, Program.cs
│   └── FIAP.CatalogAPI.Tests/           # Testes unitários
├── k8s/                                 # Manifestos Kubernetes deste serviço
│   ├── configmap.yaml                   # Configurações não-sensíveis
│   ├── deployment.yaml                  # Deployment com 2 réplicas
│   └── service.yaml                     # ClusterIP Service
├── Dockerfile                           # Multi-stage build
└── .dockerignore
```

---

## 3. Fluxo de Compra

```
Cliente → POST /api/purchase
              ↓
         CatalogAPI
         - Valida jogo + usuário (JWT)
         - Verifica se já possui
         - Gera CorrelationId
         - Publica OrderPlacedEvent → tópico order-placed
              ↓
         202 Accepted { CorrelationId }

         (assíncrono)
         PaymentAPI consome order-placed
         PaymentAPI publica PaymentProcessedEvent → payment-processed

         CatalogAPI consome payment-processed
         - Se Approved → adiciona jogo à Library do usuário
         - Se Rejected → apenas loga
```

**Tópicos Kafka:**

| Tópico | Publicado por | Consumido por |
|---|---|---|
| `order-placed` | CatalogAPI | PaymentAPI |
| `payment-processed` | PaymentAPI | CatalogAPI |

---

### Consumidor Kafka e a subida da API

Os consumidores são `BackgroundService`, e a primeira instrução do `ExecuteAsync` é `await Task.Yield()`. Ela não é decorativa: `consumer.Consume(stoppingToken)` é uma chamada **bloqueante**, e sem devolver o controle ao host antes do laço o `StartAsync` nunca retorna — o Kestrel não chega a fazer bind e a API sobe sem servidor HTTP.

O sintoma é traiçoeiro porque só aparece quando o tópico **já existe e está vazio**: aí o `Consume` fica esperando mensagem, em vez de lançar `Unknown topic or partition` de imediato e cair no `await Task.Delay` do `catch`, que por acidente devolvia o controle e deixava a aplicação subir. Na prática, quebrava em todo restart posterior ao primeiro evento publicado — o container ficava `Up`, mas `/health` não respondia e o target correspondente aparecia `down` no Prometheus.

## 4. Autenticação com Keycloak

Todos os endpoints protegidos exigem um **Bearer JWT** emitido pelo Keycloak, realm `TechChallengeFiap`. A CatalogAPI apenas **valida** o token — quem emite é o Keycloak e quem faz o login é a `UsersAPI`. Este serviço não tem `AuthController` nem client secret: precisa somente de `Keycloak__Authority` e `Keycloak__Audience`.

| Variável | Valor local (`dotnet run`) | Valor no cluster |
|---|---|---|
| `Keycloak__Authority` | `http://localhost:8081/realms/TechChallengeFiap` | `http://keycloak/realms/TechChallengeFiap` |
| `Keycloak__Audience` | `tech-challenge` | `tech-challenge` |

> **O `Authority` precisa bater exatamente com o claim `iss` do token.** O Keycloak monta esse claim a partir do header `Host` da requisição, e clientes HTTP omitem a porta quando ela é a padrão — por isso o valor do cluster vai **sem** `:80`, enquanto o local carrega o `:8081` que o Docker Compose publica. Divergência aqui produz 401 em toda rota protegida, sem indicação da causa.

### Preparar o usuário (primeira vez)

O realm é importado com clients e roles, mas **sem usuários**. Crie um em `http://localhost:8081` (usuário e senha de admin definidos no `.env` do `FIAP.Orchestration`) → realm `TechChallengeFiap` → **Users** → **Add user**, depois aba **Credentials** → **Set password** com *Temporary* desmarcado.

O realm exige dois atributos customizados, `DateOfBirth` e `Gender`; sem eles a criação falha com 400. Para as rotas de gerenciamento de usuário da `UsersAPI`, atribua ainda a role `Admin` do client `tech-challenge` em **Role mapping**.

### Obter token

Pelo endpoint de login da `UsersAPI` (form-data, não JSON):

```bash
curl -s -X POST http://localhost:5002/api/Auth/login   -F "Username=demo.fase3" -F "Password=Demo@12345"
```

Ou direto no Keycloak:

```bash
curl -s -X POST http://localhost:8081/realms/TechChallengeFiap/protocol/openid-connect/token   -d "client_id=tech-challenge" -d "grant_type=password"   -d "username=demo.fase3" -d "password=Demo@12345" -d "client_secret=<secret>"
```

O `sub` do token é usado automaticamente como `UserId` no endpoint de compra.

Em ambiente com API Gateway, o login sai pelo Kong (`http://localhost:30080/api/auth/login`) e o token vale para os dois serviços — ver [`FIAP.Orchestration/README.md` § 10](../FIAP.Orchestration/README.md#10-api-gateway-kong).

---

## 5. Requisitos de Orquestração — Validação

| Requisito | Status | Evidência |
|---|---|---|
| Manifestos na pasta `/k8s` da raiz do repositório | ✅ | `FIAP.CatalogAPI/k8s/` |
| Uso de **Deployment** para gerenciar Pods | ✅ | `k8s/deployment.yaml` — `kind: Deployment`, 2 réplicas |
| Pods isolados **não utilizados** | ✅ | Nenhum `kind: Pod` nos manifestos |
| **ConfigMap** para configurações não-sensíveis | ✅ | `k8s/configmap.yaml` — Kafka topics, URLs, environment |
| **Secret** para dados sensíveis | ✅ | `catalog-api-secret`, criado fora do Git por `FIAP.Orchestration/scripts/create-k8s-secrets.sh` e injetado via `envFrom` |

### O que cada manifesto armazena

**ConfigMap** (`k8s/configmap.yaml`) — dados que podem ser versionados:
```yaml
Kafka__BootstrapServers: "kafka:9092"
Kafka__TopicOrderPlaced: "order-placed"
MongoDb__Host: "mongodb"
ASPNETCORE_ENVIRONMENT: "Production"
```

**Secret** (`catalog-api-secret`) — dados sensíveis, **sem manifesto versionado**. Chaves injetadas em tempo de execução:
```text
ConnectionStrings__DefaultConnection
MongoDb__Username
MongoDb__Password
Elasticsearch__ApiKey        # opcional
```

`Elasticsearch__ApiKey` só é necessária ao apontar `Elasticsearch__Uri` para um endpoint gerenciado (Elastic Cloud / Amazon OpenSearch) — o cluster local e o do Kubernetes sobem com `xpack.security.enabled=false`, sem credencial. Também aceita `Elasticsearch__Username` / `Elasticsearch__Password` no lugar da API key.

Para rodar com `dotnet run`, os mesmos valores ficam em User Secrets, fora do repositório:
```powershell
cd src/FIAP.CatalogAPI.Api
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost,1433;Database=CatalogAPI;User Id=sa;Password=<senha>;TrustServerCertificate=True"
dotnet user-secrets set "MongoDb:Username" "<usuario>"
dotnet user-secrets set "MongoDb:Password" "<senha>"
```

O `docker-compose.yml` deste repositório lê as credenciais de um `.env` fora do git; use o `.env.example` como base.

---

## 6. Manifestos Kubernetes — CatalogAPI

### `k8s/deployment.yaml`
- `kind: Deployment` com **2 réplicas**
- `imagePullPolicy: IfNotPresent` (usa imagem local em desenvolvimento)
- `envFrom` carrega ConfigMap + Secret como variáveis de ambiente
- `readinessProbe` e `livenessProbe` em `/health`
- Resource limits: 100m–500m CPU / 128Mi–512Mi memória

### `k8s/service.yaml`
- `type: ClusterIP` — acessível apenas dentro do cluster
- Porta `80` → container `8080`
- Outros serviços chamam via DNS: `http://catalog-api:80`

### `k8s/configmap.yaml`
- Configurações não-sensíveis do serviço

> **Infraestrutura compartilhada** (SQL Server, MongoDB, Redis, Kafka, Keycloak) está em `FIAP.Orchestration/k8s/` — o repositório central de orquestração.

---

## 7. Deploy — Passo a Passo Completo

### Pré-requisitos

- [Docker Desktop](https://www.docker.com/products/docker-desktop/) instalado e rodando
- Kubernetes habilitado no Docker Desktop (Settings → Kubernetes → Enable Kubernetes)
- `kubectl` disponível: `kubectl version --client`

Verifique que o cluster está acessível:
```powershell
kubectl cluster-info
# Esperado: "Kubernetes control plane is running at https://127.0.0.1:..."
```

---

### Passo 1 — Build da imagem Docker

Na raiz do repositório `FIAP.CatalogAPI`:

```powershell
cd C:\Users\mathf\source\repos\FIAP.CatalogAPI

docker build -t fiap/catalog-api:latest .
```

Verifique que a imagem foi criada:
```powershell
docker images fiap/catalog-api
```

---

### Passo 2 — Aplicar infraestrutura compartilhada (Orchestration)

A infraestrutura (SQL Server, MongoDB, Kafka, etc.) é responsabilidade do `FIAP.Orchestration`.
Aplique tudo com um único comando via **Kustomize**:

```powershell
cd C:\Users\mathf\source\repos\FIAP.Orchestration

kubectl apply -k k8s/
```

Aguarde os pods subirem:
```powershell
kubectl get pods -n fiap-games -w
```

Todos devem chegar ao status `Running` antes de continuar:
```
NAME                           READY   STATUS    RESTARTS
sqlserver-xxx                  1/1     Running   0
mongodb-xxx                    1/1     Running   0
kafka-xxx                      1/1     Running   0
catalog-api-xxx                1/1     Running   0
```

---

### Passo 3 — Aplicar manifestos do CatalogAPI (opcional)

Os manifestos em `FIAP.CatalogAPI/k8s/` são para deploy isolado do serviço,
útil quando você atualiza apenas o CatalogAPI sem mexer na infraestrutura:

```powershell
cd C:\Users\mathf\source\repos\FIAP.CatalogAPI

# Aplicar namespace (se ainda não existir)
kubectl apply -f https://raw.githubusercontent.com/Projeto-Pos-Tech-FIAP/FIAP.Orchestration/main/k8s/00-namespace.yaml

# Criar os Secrets a partir do .env do FIAP.Orchestration
bash ..\FIAP.Orchestration\scripts\create-k8s-secrets.sh

# Aplicar os manifestos do CatalogAPI
kubectl apply -f k8s/configmap.yaml
kubectl apply -f k8s/deployment.yaml
kubectl apply -f k8s/service.yaml
```

---

### Passo 4 — Verificar o deploy

```powershell
# Ver todos os recursos do namespace
kubectl get all -n fiap-games

# Ver detalhes do deployment
kubectl describe deployment catalog-api -n fiap-games

# Ver logs da aplicação
kubectl logs -f deployment/catalog-api -n fiap-games
```

---

### Passo 5 — Acessar a API

Os Services usam `ClusterIP` (apenas interno ao cluster). Para acessar pelo navegador:

```powershell
kubectl port-forward svc/catalog-api 5001:80 -n fiap-games
```

Abrir: **http://localhost:5001/swagger**

---

### Passo 6 — Atualizar após mudança no código

Quando fizer alterações no código e quiser atualizar o deploy:

```powershell
# 1. Rebuildar a imagem com nova tag de versão
docker build -t fiap/catalog-api:v2 .

# 2. Fazer rolling update sem downtime
kubectl set image deployment/catalog-api catalog-api=fiap/catalog-api:v2 -n fiap-games

# 3. Acompanhar o rollout
kubectl rollout status deployment/catalog-api -n fiap-games

# 4. Em caso de problema, fazer rollback
kubectl rollout undo deployment/catalog-api -n fiap-games
```

---

### Comandos de Diagnóstico

```powershell
# Ver eventos de um pod com problema
kubectl describe pod <nome-do-pod> -n fiap-games

# Entrar no container para debug
kubectl exec -it <nome-do-pod> -n fiap-games -- /bin/sh

# Escalar réplicas
kubectl scale deployment/catalog-api --replicas=3 -n fiap-games

# Remover apenas o CatalogAPI
kubectl delete -f k8s/ -n fiap-games

# Remover tudo (infraestrutura + serviços)
kubectl delete -k C:\Users\mathf\source\repos\FIAP.Orchestration\k8s\
```

---

## 8. Executar Localmente com Docker Compose

Para desenvolvimento local sem Kubernetes:

```powershell
cd C:\Users\mathf\source\repos\FIAP.Orchestration

# Subir toda a stack (infra + CatalogAPI)
docker-compose up -d --build

# Acompanhar logs
docker-compose logs -f catalog-api

# Parar tudo
docker-compose down
```

### Endereços

| Serviço | URL | Credenciais |
|---|---|---|
| CatalogAPI Swagger | http://localhost:5001/swagger | — |
| Keycloak Admin | http://localhost:8180 | definidas no `.env` |
| Kafka UI | http://localhost:8090 | — |
| SQL Server | localhost:1433 | `sa` / definida no `.env` |
| MongoDB | localhost:27018 | definidas no `.env` |
| Redis | localhost:6379 | — |
| Elasticsearch | http://localhost:9200 | — |

---

## 9. Endpoints da API

### Auth

| Método | Endpoint | Descrição | Auth |
|---|---|---|---|
| POST | `/api/auth/login` | Obtém token JWT do Keycloak | ❌ |
| POST | `/api/auth/refresh` | Renova o access token | ❌ |

### Games

| Método | Endpoint | Descrição | Auth |
|---|---|---|---|
| GET | `/api/game` | Lista todos os jogos | ❌ |
| GET | `/api/game/{id}` | Busca jogo por ID | ❌ |
| POST | `/api/game` | Cria novo jogo | ✅ |
| PUT | `/api/game/{id}` | Atualiza jogo | ✅ |
| DELETE | `/api/game/{id}` | Remove jogo (soft delete) | ✅ |

### Search (Elasticsearch)

| Método | Endpoint | Descrição | Auth |
|---|---|---|---|
| GET | `/api/search?q=&genre=&minPrice=&maxPrice=&onlyActive=&page=&pageSize=` | Busca fuzzy ordenada por relevância | ❌ |
| POST | `/api/search/reindex` | Recarrega o índice a partir do SQL Server | ✅ |

### Purchase

| Método | Endpoint | Body | Descrição | Auth |
|---|---|---|---|---|
| POST | `/api/purchase` | `{ "gameId": 1 }` | Inicia compra — UserId extraído do JWT | ✅ |

### Event Logs (MongoDB)

| Método | Endpoint | Descrição | Auth |
|---|---|---|---|
| GET | `/api/eventlog?correlationId=&eventType=&service=&limit=50` | Lista os eventos mais recentes, com filtros opcionais | ✅ |
| GET | `/api/eventlog/{correlationId}` | Rastro completo de um fluxo de compra, do mais antigo ao mais recente | ✅ |

> Endpoints marcados com ✅ requerem header `Authorization: Bearer <token>`

---

## 10. Persistência Poliglota — MongoDB e Redis

### MongoDB — logs de eventos e auditoria

| Base / coleção | Conteúdo | Quem escreve |
|---|---|---|
| `FcgEvents.EventLogs` | Todo evento Kafka publicado ou consumido | CatalogAPI, PaymentAPI e UsersAPI |
| `CatalogAudit.AuditLogs` | Auditoria de alterações das entidades relacionais | CatalogAPI (interceptor de `SaveChanges` do EF Core) |

A CatalogAPI registra `OrderPlacedEvent` (publicado) e `PaymentProcessedEvent` (consumido). Como os três serviços gravam na mesma coleção, `GET /api/eventlog/{correlationId}` devolve o fluxo inteiro de uma compra — inclusive o que aconteceu dentro do PaymentAPI. Índices em `{correlationId, occurredAt}` e `{occurredAt}` são criados na subida da aplicação.

Uma falha ao gravar o log **não** derruba o fluxo de negócio: a exceção é capturada e logada — o log é observabilidade, não parte da transação.

### Redis — cache distribuído

Leitura e escrita via `IDistributedCache` (abstração do ASP.NET Core, sobre `AddStackExchangeRedisCache`); a invalidação por prefixo usa o `IConnectionMultiplexer` do StackExchange.Redis, porque varrer chaves (`SCAN`) não existe na abstração.

| O que é cacheado | Chave | Invalidação |
|---|---|---|
| `GET /api/game` — listagem completa do catálogo | `catalog:games:all` | TTL 10 min + `POST`/`PUT`/`DELETE` em jogo |
| `GET /api/game/{id}` — detalhe com join de gêneros | `catalog:games:{id}` | TTL 10 min + `POST`/`PUT`/`DELETE` em jogo |

**Redis fora do ar não derruba a API** — as operações de cache capturam exceção, logam um *warning* e seguem para o SQL Server; o multiplexer sobe com `AbortOnConnectFail = false` e reconecta em background.

Para ver o cache em ação, chame `GET /api/game` duas vezes e observe `Cache MISS` / `Cache HIT` no log (nível `Debug`, já habilitado em `appsettings.Development.json`):

```bash
docker exec fiap-redis redis-cli KEYS 'catalog:*'
```

> O `IDistributedCache` grava cada entrada como **hash** (campos `data`, `absexp`, `sldexp`), não como string — `redis-cli GET` devolve `WRONGTYPE`. Para ver o conteúdo: `redis-cli HGET catalog:games:all data`.

| Variável | Descrição | Exemplo (local) |
|---|---|---|
| `ConnectionStrings__Redis` | Endereço do Redis | `localhost:6379` |
| `Cache__InstanceName` | Prefixo das chaves | `catalog:` |
| `Cache__DefaultTtlSeconds` | TTL padrão das entradas | `600` (10 min) |
| `MongoDb__EventsDatabaseName` / `__EventsCollectionName` | Base e coleção dos logs de eventos | `FcgEvents` / `EventLogs` |

Detalhes da decisão de arquitetura em [`FIAP.Orchestration/README.md` § 12](../FIAP.Orchestration/README.md#12-persistência-poliglota--mongodb-e-redis).

---

## 11. Busca Avançada — Elasticsearch

O motor de busca é **independente da consulta relacional**: `GET /api/search` lê do índice no Elasticsearch, não do SQL Server. É o que permite tolerância a erros de digitação e ordenação por relevância — nenhum dos dois é possível com `LIKE '%termo%'`.

### Endpoint

```bash
# Fuzzy: "wticher" (letras trocadas) encontra "The Witcher 3"
curl "http://localhost:5001/api/search?q=wticher"

# Filtros combinados — recortam o resultado sem interferir no score
curl "http://localhost:5001/api/search?q=rpg&genre=RPG&minPrice=50&maxPrice=200&pageSize=5"

# Sem termo: catálogo inteiro em ordem alfabética
curl "http://localhost:5001/api/search"
```

Resposta:

```json
{
  "query": "wticher",
  "total": 2,
  "page": 1,
  "pageSize": 20,
  "tookMs": 4,
  "items": [
    { "gameId": 1, "title": "The Witcher 3", "basePrice": 99.90, "genres": ["RPG"], "score": 9.12 },
    { "gameId": 2, "title": "The Witcher 2", "basePrice": 49.90, "genres": ["RPG"], "score": 5.31 }
  ]
}
```

O campo `score` é a relevância calculada pelo Elasticsearch e define a ordem dos resultados — é o que se mostra na demonstração para provar que a ordenação não é alfabética nem por id.

### Como a relevância é montada

A consulta é um `bool` com três partes, cada uma com um papel distinto:

| Cláusula | Conteúdo | Por que |
|---|---|---|
| `must` | `multi_match` com `fuzziness: AUTO` sobre `title^4`, `genres^2`, `developer^2`, `publisher^1.5`, `description` | Casa o termo e **produz o score**. O título pesa 4× porque é o que o usuário digita; a descrição só desempata |
| `should` | `match_phrase` em `title` com `boost: 6` | Empurra o acerto **exato** para cima do acerto aproximado — quem digitou certo vem primeiro |
| `filter` | `isActive`, `genres.keyword`, `range` em `basePrice` | Recorta o resultado **sem pontuar** (filtros não entram no score) e é cacheável pelo próprio Elasticsearch |

`fuzziness: AUTO` escala a distância de Levenshtein pelo tamanho do termo — 1 edição em palavras curtas, 2 nas longas — e `fuzzy_transpositions` cobre letras adjacentes trocadas (`Clal of Duty` → `Call of Duty`) com uma única edição.

### Mapping do índice

O índice `games` é criado pela própria aplicação, com mapping explícito. Sem ele o Elasticsearch inferiria os tipos no primeiro documento e os campos de texto ficariam sem o analisador do catálogo:

| Campo | Tipo | Observação |
|---|---|---|
| `title` | `text` + `title.keyword` | O subcampo `keyword` é o que permite ordenar alfabeticamente (texto analisado não é ordenável) |
| `genres` | `text` + `genres.keyword` | O subcampo `keyword` atende o filtro por gênero, que é match exato |
| `description`, `developer`, `publisher` | `text` | Analisador do catálogo |
| `basePrice` | `double` | Filtro de faixa de preço |
| `isActive` | `boolean` | Filtro padrão da busca |
| `releaseDate` | `date` | — |

O analisador `catalog_analyzer` é `standard` + `lowercase` + `asciifolding`: "fifa" casa com "FIFA" e "sao paulo" com "São Paulo" — acento não é erro de digitação, então não deve consumir distância de edição do fuzzy.

### Sincronização com o banco principal

A indexação acontece no `GameService`, logo após a escrita no SQL Server:

| Operação | Efeito no índice |
|---|---|
| `POST /api/game` | Reindexa o jogo (relê a entidade para trazer os **nomes** dos gêneros, já que o relacionamento gravado só tem ids) |
| `PUT /api/game/{id}` | Reindexa o jogo |
| `DELETE /api/game/{id}` | Remove do índice — a busca não devolve o que a API já considera removido |

As escritas usam `refresh=true`, então o jogo recém-criado já aparece na busca seguinte (sem isso, só depois do refresh periódico de 1s). O volume de escrita do catálogo é baixo, então o custo é irrelevante e o comportamento fica previsível na demonstração.

**Falha ao indexar não derruba a escrita.** A gravação no banco principal já aconteceu; derrubar a requisição ali perderia a escrita. A exceção é logada e o índice é reconciliado depois:

```bash
# Carga inicial (catálogo que já existia antes do Elasticsearch entrar)
# e reconciliação após indisponibilidade do cluster
curl -X POST http://localhost:5001/api/search/reindex -H "Authorization: Bearer $TOKEN"
# → { "indexed": 12, "message": "12 jogo(s) reindexado(s)." }
```

O reindex deixa o índice **igual** ao banco, em três passos:

1. **Verifica o índice ignorando o cache.** O processo guarda em memória que o índice já está pronto, para não pagar um `HEAD` em cada escrita. Se o índice tiver sido perdido depois disso (cluster recriado, índice apagado, PVC limpo), confiar nesse cache faria o `_bulk` seguinte criar o índice por **mapping dinâmico** — sem o `catalog_analyzer`, com `basePrice` como `float` e `gameId` como `long`. O reindex é o caminho explícito de reconciliação, então reverifica.
2. **Grava todos os jogos em um único `_bulk`.**
3. **Apaga os órfãos** com `_delete_by_query` (`must_not` nos ids que ainda existem). Sem este passo o `_bulk` só sobrescreveria os ids vivos, e um jogo deletado enquanto o cluster estava fora sobreviveria no índice como fantasma, voltando nas buscas.

### Comportamento com o cluster fora do ar

Diferente do cache, a busca **não** tem equivalente no banco relacional — fuzzy e score não existem em SQL. Então o endpoint falha explicitamente em vez de devolver silenciosamente um resultado pior:

| Situação | Resposta |
|---|---|
| Cluster indisponível | `503` — `Busca indisponível — cluster inalcançável: Connection refused (...)` |
| Elasticsearch recusa a consulta | `503` — `Busca indisponível — Elasticsearch respondeu HTTP 4xx/5xx: ...` |
| Índice ainda não criado | `200` com `total: 0` e um *warning* no log pedindo o reindex |
| Falha ao indexar em `POST`/`PUT`/`DELETE` | A escrita no SQL Server é mantida (`201`/`200`/`204`); erro no log dizendo qual jogo ficou de fora e para rodar o reindex |

As mensagens distinguem **cluster inalcançável** (falha de transporte, sem status HTTP) de **Elasticsearch recusou** (respondeu com status) — é o que diz ao operador se o problema é de rede ou de requisição. E o log da falha de escrita nomeia o jogo e a ação:

```
fail: Jogo 8 gravado no banco mas não indexado (cluster inalcançável: Connection refused (localhost:9200)).
      Rode POST /api/search/reindex.
```

O índice é criado no start da aplicação por um `BackgroundService` com retry (10 tentativas, 5s de intervalo), porque em Kubernetes e no docker-compose a API quase sempre sobe antes do Elasticsearch ficar pronto — e isso não pode impedir a API de atender.

### Configuração

Nada é hardcoded: a mesma imagem serve para o cluster local, para o serviço no Kubernetes e para um endpoint gerenciado (**Elastic Cloud** ou **Amazon OpenSearch**) — basta trocar as variáveis.

| Variável | Descrição | Exemplo (local) |
|---|---|---|
| `Elasticsearch__Uri` | Endereço do cluster | `http://localhost:9200` |
| `Elasticsearch__GamesIndex` | Nome do índice | `games` |
| `Elasticsearch__Fuzziness` | Distância de edição do fuzzy | `AUTO` |
| `Elasticsearch__RequestTimeoutSeconds` | Timeout das requisições | `10` |
| `Elasticsearch__ApiKey` | API Key do Elastic Cloud (**Secret**) | — |
| `Elasticsearch__Username` / `__Password` | Autenticação básica (**Secret**) | — |
| `Elasticsearch__AllowInvalidCertificate` | Aceita certificado self-signed | `false` |

A `ApiKey` tem precedência sobre usuário/senha. Credencial nenhuma vive em código ou YAML versionado: `Uri`, `GamesIndex`, `Fuzziness` e `RequestTimeoutSeconds` ficam no ConfigMap; `ApiKey`, `Username` e `Password` entram pela Secret da API.

### Inspecionar o índice

```bash
# Saúde do cluster
curl http://localhost:9200/_cluster/health?pretty

# Mapping efetivamente aplicado
curl http://localhost:9200/games/_mapping?pretty

# Quantos documentos estão indexados
curl http://localhost:9200/games/_count?pretty

# Ver o score de cada hit direto no Elasticsearch
curl "http://localhost:9200/games/_search?q=title:wticher~&pretty"
```
