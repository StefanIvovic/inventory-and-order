using System.Text.RegularExpressions;

namespace BuildingBlocks.Messaging;

public sealed partial record MessagingSettings
{
    public MessagingSettings(string serviceName, string postgresConnectionString, Uri rabbitMqUri)
    {
        if (string.IsNullOrWhiteSpace(serviceName) || !ServiceNamePattern().IsMatch(serviceName))
        {
            throw new ArgumentException("Service name must contain only lowercase letters, digits and '-'.", nameof(serviceName));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(postgresConnectionString);
        ArgumentNullException.ThrowIfNull(rabbitMqUri);
        if (rabbitMqUri.Scheme is not ("amqp" or "amqps"))
        {
            throw new ArgumentException("RabbitMQ URI must use amqp or amqps.", nameof(rabbitMqUri));
        }

        ServiceName = serviceName;
        PostgresConnectionString = postgresConnectionString;
        RabbitMqUri = rabbitMqUri;
    }

    public string ServiceName { get; }

    public string PostgresConnectionString { get; }

    public Uri RabbitMqUri { get; }

    [GeneratedRegex("^[a-z0-9-]+$")]
    private static partial Regex ServiceNamePattern();
}
