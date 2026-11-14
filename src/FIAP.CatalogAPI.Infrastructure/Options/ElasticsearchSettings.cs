namespace FIAP.CatalogAPI.Infrastructure.Options;

/// <summary>
/// Conexão com o cluster de busca. Tudo vem de configuração para que a mesma imagem
/// aponte para o Elasticsearch local (docker-compose), para o serviço no cluster
/// Kubernetes ou para um endpoint gerenciado (Elastic Cloud / Amazon OpenSearch) sem
/// recompilar — e para que credencial nenhuma viva no código.
/// </summary>
public class ElasticsearchSettings
{
    public const string SectionName = "Elasticsearch";

    /// <summary>Endereço do cluster. Ex.: http://localhost:9200 ou https://....es.cloud:9243.</summary>
    public string Uri { get; set; } = "http://localhost:9200";

    /// <summary>Nome do índice de jogos.</summary>
    public string GamesIndex { get; set; } = "games";

    /// <summary>Usuário para autenticação básica. Vazio = cluster sem segurança (dev local).</summary>
    public string? Username { get; set; }

    public string? Password { get; set; }

    /// <summary>API Key do Elastic Cloud. Tem precedência sobre usuário/senha quando preenchida.</summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Aceita certificado self-signed. Necessário no Elasticsearch com security habilitado
    /// dentro do cluster; deve ser false contra um endpoint gerenciado.
    /// </summary>
    public bool AllowInvalidCertificate { get; set; }

    /// <summary>Timeout das requisições ao cluster, em segundos.</summary>
    public int RequestTimeoutSeconds { get; set; } = 10;

    /// <summary>
    /// Distância de edição (Levenshtein) da busca fuzzy. "AUTO" deixa o Elasticsearch
    /// escalar a tolerância pelo tamanho do termo — 1 erro em palavras curtas, 2 nas longas.
    /// </summary>
    public string Fuzziness { get; set; } = "AUTO";
}
