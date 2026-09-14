# FIAP.CatalogAPI

Microserviço de catálogo de jogos da plataforma FIAP Games. Responsável pelo CRUD de jogos e pelo fluxo de compra via Apache Kafka, com autenticação via Keycloak.

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
│   ├── secret.yaml                      # Dados sensíveis (connection strings)
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

O realm é importado com clients e roles, mas **sem usuários**. Crie um em `http://localhost:8081` (admin / admin123) → realm `TechChallengeFiap` → **Users** → **Add user**, depois aba **Credentials** → **Set password** com *Temporary* desmarcado.

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
| **Secret** para dados sensíveis | ✅ | `k8s/secret.yaml` — connection string SQL, senha MongoDB |

### O que cada manifesto armazena

**ConfigMap** (`k8s/configmap.yaml`) — dados que podem ser versionados:
```yaml
Kafka__BootstrapServers: "kafka:9092"
Kafka__TopicOrderPlaced: "order-placed"
MongoDb__Host: "mongodb"
ASPNETCORE_ENVIRONMENT: "Production"
```

**Secret** (`k8s/secret.yaml`) — dados sensíveis, **nunca commitar com valores reais**:
```yaml
ConnectionStrings__DefaultConnection: "Server=sqlserver,1433;..."
MongoDb__Username: "admin"
MongoDb__Password: "PosTech@123"
```

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

### `k8s/secret.yaml`
- Credenciais e connection strings

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

# Aplicar os manifestos do CatalogAPI
kubectl apply -f k8s/configmap.yaml
kubectl apply -f k8s/secret.yaml
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
| Keycloak Admin | http://localhost:8180 | admin / PosTech@123 |
| Kafka UI | http://localhost:8090 | — |
| SQL Server | localhost:1433 | sa / PosTech@123 |
| MongoDB | localhost:27018 | admin / PosTech@123 |
| Redis | localhost:6379 | — |

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
