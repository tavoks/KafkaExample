using System.ComponentModel.DataAnnotations;

namespace Common.Infrastructure
{
    public class OutboxMessage
    {
        /// <summary>
        /// Identificador único da mensagem outbox
        /// </summary>
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Tipo do evento (ex: "OrderCreated", "PaymentProcessed")
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string EventType { get; set; } = string.Empty;

        /// <summary>
        /// Conteúdo JSON serializado do evento
        /// </summary>
        [Required]
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// Timestamp de quando a mensagem foi criada na tabela outbox
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Timestamp de quando a mensagem foi processada (publicada no Kafka)
        /// Null significa que ainda não foi processada
        /// </summary>
        public DateTime? ProcessedAt { get; set; }

        /// <summary>
        /// Nome do tópico Kafka onde a mensagem foi publicada
        /// Preenchido após o processamento bem-sucedido
        /// </summary>
        [MaxLength(255)]
        public string? TopicName { get; set; }

        /// <summary>
        /// Chave da partição Kafka (opcional)
        /// Usado para garantir ordenação de mensagens relacionadas
        /// </summary>
        [MaxLength(500)]
        public string? PartitionKey { get; set; }

        /// <summary>
        /// Número de tentativas de processamento
        /// Usado para implementar retry logic e dead letter queue
        /// </summary>
        public int RetryCount { get; set; } = 0;

        /// <summary>
        /// Timestamp da última tentativa de processamento
        /// </summary>
        public DateTime? LastRetryAt { get; set; }

        /// <summary>
        /// Mensagem de erro da última tentativa (se houver)
        /// </summary>
        public string? LastError { get; set; }

        /// <summary>
        /// Identifica se a mensagem deve ser ignorada (soft delete)
        /// Usado para mensagens que falharam múltiplas vezes
        /// </summary>
        public bool IsIgnored { get; set; } = false;

        /// <summary>
        /// Versão do schema do evento para controle de evolução
        /// </summary>
        public int SchemaVersion { get; set; } = 1;

        /// <summary>
        /// Metadados adicionais em formato JSON (opcional)
        /// Pode conter informações como correlation ID, user ID, etc.
        /// </summary>
        public string? Metadata { get; set; }

        /// <summary>
        /// Data de expiração da mensagem (opcional)
        /// Mensagens expiradas podem ser ignoradas ou movidas para dead letter
        /// </summary>
        public DateTime? ExpiresAt { get; set; }

        /// <summary>
        /// Indica se a mensagem já foi processada com sucesso
        /// </summary>
        public bool IsProcessed => ProcessedAt.HasValue;

        /// <summary>
        /// Indica se a mensagem falhou múltiplas vezes
        /// </summary>
        public bool HasExceededMaxRetries(int maxRetries = 3) => RetryCount >= maxRetries;

        /// <summary>
        /// Indica se a mensagem está expirada
        /// </summary>
        public bool IsExpired => ExpiresAt.HasValue && DateTime.UtcNow > ExpiresAt.Value;

        /// <summary>
        /// Marca a mensagem como processada com sucesso
        /// </summary>
        /// <param name="topicName">Nome do tópico onde foi publicada</param>
        public void MarkAsProcessed(string topicName)
        {
            ProcessedAt = DateTime.UtcNow;
            TopicName = topicName;
            LastError = null;
        }

        /// <summary>
        /// Incrementa o contador de retry e registra o erro
        /// </summary>
        /// <param name="errorMessage">Mensagem de erro</param>
        public void IncrementRetry(string errorMessage)
        {
            RetryCount++;
            LastRetryAt = DateTime.UtcNow;
            LastError = errorMessage;
        }

        /// <summary>
        /// Marca a mensagem para ser ignorada
        /// </summary>
        /// <param name="reason">Motivo para ignorar</param>
        public void MarkAsIgnored(string reason)
        {
            IsIgnored = true;
            LastError = reason;
        }
    }

    /// <summary>
    /// Extensões para facilitar o trabalho com OutboxMessage
    /// </summary>
    public static class OutboxMessageExtensions
    {
        /// <summary>
        /// Cria uma nova instância de OutboxMessage para um evento
        /// </summary>
        /// <typeparam name="T">Tipo do evento</typeparam>
        /// <param name="eventData">Dados do evento</param>
        /// <param name="eventType">Tipo do evento (opcional, será inferido do tipo T se não fornecido)</param>
        /// <param name="partitionKey">Chave de partição (opcional)</param>
        /// <param name="metadata">Metadados adicionais (opcional)</param>
        /// <param name="expiresAt">Data de expiração (opcional)</param>
        /// <returns>Nova instância de OutboxMessage</returns>
        public static OutboxMessage CreateForEvent<T>(
            T eventData,
            string? eventType = null,
            string? partitionKey = null,
            string? metadata = null,
            DateTime? expiresAt = null) where T : class
        {
            return new OutboxMessage
            {
                EventType = eventType ?? typeof(T).Name,
                Content = System.Text.Json.JsonSerializer.Serialize(eventData),
                PartitionKey = partitionKey,
                Metadata = metadata,
                ExpiresAt = expiresAt
            };
        }

        /// <summary>
        /// Deserializa o conteúdo da mensagem para o tipo especificado
        /// </summary>
        /// <typeparam name="T">Tipo do evento</typeparam>
        /// <param name="message">Mensagem outbox</param>
        /// <returns>Objeto deserializado</returns>
        public static T? DeserializeContent<T>(this OutboxMessage message) where T : class
        {
            if (string.IsNullOrEmpty(message.Content)) 
                return null;

            try
            {
                return System.Text.Json.JsonSerializer.Deserialize<T>(message.Content);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Obtém as mensagens prontas para processamento
        /// </summary>
        /// <param name="query">Query de OutboxMessage</param>
        /// <param name="maxRetries">Número máximo de tentativas</param>
        /// <param name="batchSize">Tamanho do lote</param>
        /// <returns>Mensagens prontas para processamento</returns>
        public static IQueryable<OutboxMessage> ReadyForProcessing(
            this IQueryable<OutboxMessage> query,
            int maxRetries = 3,
            int batchSize = 100)
        {
            return query
                .Where(m => !m.IsProcessed &&
                            !m.IsIgnored &&
                            (!m.ExpiresAt.HasValue || m.ExpiresAt > DateTime.UtcNow) &&
                            m.RetryCount < maxRetries)
                     .OrderBy(m => m.CreatedAt)
                     .Take(batchSize);
        }
    }
}
