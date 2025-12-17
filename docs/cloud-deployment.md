# Cloud Deployment Guide

This guide covers deploying applications using Tika.BatchIngestor to major cloud platforms with optimized configurations for cloud databases.

## Table of Contents

- [Overview](#overview)
- [Amazon Web Services (AWS)](#amazon-web-services-aws)
- [Microsoft Azure](#microsoft-azure)
- [Google Cloud Platform (GCP)](#google-cloud-platform-gcp)
- [Cloud-Agnostic Best Practices](#cloud-agnostic-best-practices)
- [Container Deployment](#container-deployment)
- [Kubernetes Deployment](#kubernetes-deployment)
- [Serverless Deployment](#serverless-deployment)
- [Monitoring and Observability](#monitoring-and-observability)
- [Cost Optimization](#cost-optimization)

## Overview

Tika.BatchIngestor is cloud-ready with built-in support for major cloud database services. Key features for cloud deployment:

- **Optimized Dialects**: Aurora PostgreSQL, Aurora MySQL, Azure SQL
- **Connection Resilience**: Retry policies with exponential backoff
- **Resource Control**: CPU and memory throttling for shared environments
- **Health Checks**: Native ASP.NET Core health checks for load balancers
- **Metrics**: Real-time performance monitoring

## Amazon Web Services (AWS)

### Aurora PostgreSQL

Amazon Aurora PostgreSQL is a cloud-optimized PostgreSQL-compatible database with high performance and availability.

#### Configuration

```csharp
using Npgsql;
using Tika.BatchIngestor;
using Tika.BatchIngestor.Dialects;

// Connection string (use AWS Secrets Manager in production)
var connectionString = "Host=mydb.cluster-xyz.us-east-1.rds.amazonaws.com;" +
                       "Port=5432;" +
                       "Database=mydb;" +
                       "Username=admin;" +
                       "Password=***;" +
                       "Pooling=true;" +
                       "Minimum Pool Size=4;" +
                       "Maximum Pool Size=20;" +
                       "Connection Idle Lifetime=300;" +
                       "Connection Pruning Interval=10;" +
                       "Timeout=30;" +
                       "Command Timeout=300";

var connectionFactory = new SimpleConnectionFactory(
    connectionString,
    () => new NpgsqlConnection(connectionString)
);

// Aurora-optimized configuration
var options = new BatchIngestOptions
{
    BatchSize = 5000,              // Aurora handles large batches well
    MaxDegreeOfParallelism = 6,    // Good for Aurora's throughput
    MaxInFlightBatches = 15,
    CommandTimeoutSeconds = 600,
    EnableCpuThrottling = true,
    MaxCpuPercent = 80.0,
    UseTransactions = true,
    TransactionPerBatch = true,

    // Aurora-specific retry policy
    RetryPolicy = new RetryPolicy
    {
        MaxRetries = 5,
        InitialDelayMs = 200,
        MaxDelayMs = 10000,
        UseExponentialBackoff = true,
        UseJitter = true
    }
};

var ingestor = new BatchIngestor<MyData>(
    connectionFactory,
    new AuroraPostgreSqlDialect(),  // Aurora-optimized dialect
    mapper,
    options
);
```

#### Best Practices

1. **Connection Management**:
   - Use connection pooling (enabled by default in Npgsql)
   - Size pool: `Maximum Pool Size >= MaxDegreeOfParallelism + buffer`
   - Enable connection idle lifetime for auto-refresh

2. **Aurora-Specific Settings**:
   - Use cluster endpoint for reads and writes
   - Use reader endpoint for read-only operations
   - Enable Performance Insights for monitoring

3. **Network**:
   - Deploy application in same VPC as Aurora cluster
   - Use VPC endpoints to avoid internet gateway costs
   - Consider Aurora Global Database for multi-region

4. **Security**:
   - Use AWS Secrets Manager for connection strings
   - Enable IAM database authentication
   - Use SSL/TLS for connections

**Example with AWS Secrets Manager**:

```csharp
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using System.Text.Json;

public class AuroraConnectionFactory : IConnectionFactory
{
    private readonly IAmazonSecretsManager _secretsManager;
    private readonly string _secretName;
    private string? _cachedConnectionString;
    private DateTime _cacheExpiry;

    public AuroraConnectionFactory(IAmazonSecretsManager secretsManager, string secretName)
    {
        _secretsManager = secretsManager;
        _secretName = secretName;
    }

    public async Task<DbConnection> CreateConnectionAsync(CancellationToken ct)
    {
        // Refresh connection string from Secrets Manager every 5 minutes
        if (_cachedConnectionString == null || DateTime.UtcNow > _cacheExpiry)
        {
            var response = await _secretsManager.GetSecretValueAsync(
                new GetSecretValueRequest { SecretId = _secretName }, ct);

            var secret = JsonSerializer.Deserialize<Dictionary<string, string>>(
                response.SecretString);

            _cachedConnectionString = $"Host={secret["host"]};" +
                                      $"Port={secret["port"]};" +
                                      $"Database={secret["dbname"]};" +
                                      $"Username={secret["username"]};" +
                                      $"Password={secret["password"]};" +
                                      "Pooling=true;Maximum Pool Size=20";

            _cacheExpiry = DateTime.UtcNow.AddMinutes(5);
        }

        var connection = new NpgsqlConnection(_cachedConnectionString);
        await connection.OpenAsync(ct);
        return connection;
    }
}
```

#### Aurora MySQL

```csharp
using MySqlConnector;
using Tika.BatchIngestor.Dialects;

var connectionString = "Server=mydb.cluster-xyz.us-east-1.rds.amazonaws.com;" +
                       "Port=3306;" +
                       "Database=mydb;" +
                       "User=admin;" +
                       "Password=***;" +
                       "Pooling=true;" +
                       "MinimumPoolSize=4;" +
                       "MaximumPoolSize=20;" +
                       "ConnectionIdleTimeout=300;" +
                       "ConnectionTimeout=30;" +
                       "DefaultCommandTimeout=300";

var connectionFactory = new SimpleConnectionFactory(
    connectionString,
    () => new MySqlConnection(connectionString)
);

var options = new BatchIngestOptions
{
    BatchSize = 3000,              // Aurora MySQL sweet spot
    MaxDegreeOfParallelism = 4,
    MaxInFlightBatches = 12,
    CommandTimeoutSeconds = 600,
    EnableCpuThrottling = true,
    MaxCpuPercent = 80.0
};

var ingestor = new BatchIngestor<MyData>(
    connectionFactory,
    new AuroraMySqlDialect(),      // Aurora MySQL-optimized
    mapper,
    options
);
```

### AWS ECS (Elastic Container Service)

#### Task Definition

```json
{
  "family": "batch-ingestor-service",
  "taskRoleArn": "arn:aws:iam::123456789012:role/BatchIngestorTaskRole",
  "executionRoleArn": "arn:aws:iam::123456789012:role/ecsTaskExecutionRole",
  "networkMode": "awsvpc",
  "containerDefinitions": [
    {
      "name": "batch-ingestor",
      "image": "123456789012.dkr.ecr.us-east-1.amazonaws.com/batch-ingestor:latest",
      "cpu": 1024,
      "memory": 2048,
      "essential": true,
      "environment": [
        {
          "name": "ASPNETCORE_ENVIRONMENT",
          "value": "Production"
        },
        {
          "name": "BatchIngestor__MaxCpuPercent",
          "value": "80"
        }
      ],
      "secrets": [
        {
          "name": "ConnectionStrings__Database",
          "valueFrom": "arn:aws:secretsmanager:us-east-1:123456789012:secret:aurora-connection"
        }
      ],
      "logConfiguration": {
        "logDriver": "awslogs",
        "options": {
          "awslogs-group": "/ecs/batch-ingestor",
          "awslogs-region": "us-east-1",
          "awslogs-stream-prefix": "ecs"
        }
      },
      "healthCheck": {
        "command": ["CMD-SHELL", "curl -f http://localhost/health || exit 1"],
        "interval": 30,
        "timeout": 5,
        "retries": 3,
        "startPeriod": 60
      }
    }
  ],
  "requiresCompatibilities": ["FARGATE"],
  "cpu": "1024",
  "memory": "2048"
}
```

#### Application Configuration (appsettings.Production.json)

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Tika.BatchIngestor": "Warning"
    }
  },
  "BatchIngestor": {
    "BatchSize": 5000,
    "MaxDegreeOfParallelism": 6,
    "MaxInFlightBatches": 15,
    "EnableCpuThrottling": true,
    "MaxCpuPercent": 80.0,
    "EnablePerformanceMetrics": true,
    "CommandTimeoutSeconds": 600
  },
  "HealthChecks": {
    "Enabled": true,
    "Endpoint": "/health"
  }
}
```

### AWS Lambda (Serverless)

For batch processing triggered by events:

```csharp
using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

public class Function
{
    private readonly IBatchIngestor<SensorData> _ingestor;

    public Function()
    {
        // Initialize once (Lambda container reuse)
        var connectionString = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING");
        var connectionFactory = new SimpleConnectionFactory(
            connectionString,
            () => new NpgsqlConnection(connectionString)
        );

        var options = new BatchIngestOptions
        {
            BatchSize = 2000,
            MaxDegreeOfParallelism = 2,    // Lambda vCPU constraints
            MaxInFlightBatches = 5,        // Lambda memory constraints
            EnableCpuThrottling = false,   // Lambda already throttles
            EnablePerformanceMetrics = false,
            CommandTimeoutSeconds = 300
        };

        _ingestor = new BatchIngestor<SensorData>(
            connectionFactory,
            new AuroraPostgreSqlDialect(),
            new DefaultRowMapper<SensorData>(MapSensorData),
            options
        );
    }

    public async Task FunctionHandler(SQSEvent sqsEvent, ILambdaContext context)
    {
        var data = sqsEvent.Records
            .Select(r => JsonSerializer.Deserialize<SensorData>(r.Body))
            .Where(d => d != null);

        var metrics = await _ingestor.IngestAsync(data!, "sensor_readings");

        context.Logger.LogInformation(
            $"Ingested {metrics.TotalRowsProcessed} rows in {metrics.ElapsedTime.TotalSeconds:F2}s");
    }

    private Dictionary<string, object?> MapSensorData(SensorData data)
    {
        return new Dictionary<string, object?>
        {
            ["sensor_id"] = data.SensorId,
            ["timestamp"] = data.Timestamp,
            ["value"] = data.Value
        };
    }
}
```

**Lambda Configuration**:
- Memory: 1024-2048 MB (more memory = more vCPU)
- Timeout: 5-15 minutes (depends on batch size)
- Concurrent executions: Control with reserved concurrency
- VPC: Deploy in same VPC as Aurora

## Microsoft Azure

### Azure SQL Database

Azure SQL Database is Microsoft's cloud-based SQL Server offering with built-in high availability.

#### Configuration

```csharp
using Microsoft.Data.SqlClient;
using Tika.BatchIngestor.Dialects;

// Connection string (use Azure Key Vault in production)
var connectionString = "Server=tcp:myserver.database.windows.net,1433;" +
                       "Database=mydb;" +
                       "User ID=admin@myserver;" +
                       "Password=***;" +
                       "Encrypt=True;" +
                       "TrustServerCertificate=False;" +
                       "Connection Timeout=30;" +
                       "ConnectRetryCount=3;" +
                       "ConnectRetryInterval=10;" +
                       "Min Pool Size=4;" +
                       "Max Pool Size=20;" +
                       "Pooling=true";

var connectionFactory = new SimpleConnectionFactory(
    connectionString,
    () => new SqlConnection(connectionString)
);

// Azure SQL-optimized configuration
var options = new BatchIngestOptions
{
    BatchSize = 2000,              // Azure SQL performs well here
    MaxDegreeOfParallelism = 4,
    MaxInFlightBatches = 10,
    CommandTimeoutSeconds = 300,
    EnableCpuThrottling = true,
    MaxCpuPercent = 80.0,
    UseTransactions = true,
    TransactionPerBatch = true,

    // Azure-specific retry policy
    RetryPolicy = new RetryPolicy
    {
        MaxRetries = 3,
        InitialDelayMs = 100,
        MaxDelayMs = 5000,
        UseExponentialBackoff = true,
        UseJitter = true
    }
};

var ingestor = new BatchIngestor<MyData>(
    connectionFactory,
    new AzureSqlDialect(),          // Azure SQL-optimized dialect
    mapper,
    options
);
```

#### Best Practices

1. **Service Tier Selection**:
   - **Basic/Standard**: Lower throughput, use smaller `MaxDOP` (2-4)
   - **Premium/Business Critical**: Higher throughput, use larger `MaxDOP` (4-8)
   - **Hyperscale**: Excellent for bulk ingestion

2. **Connection Management**:
   - Use connection pooling (enabled by default)
   - Enable connection resiliency (`ConnectRetryCount=3`)
   - Monitor DTU/vCore usage

3. **Performance**:
   - Disable database-level auto-tuning during bulk loads
   - Consider using staging tables with columnstore indexes
   - Monitor Query Performance Insight

4. **Security**:
   - Use Azure Key Vault for connection strings
   - Enable Azure AD authentication
   - Use Private Link for network isolation

**Example with Azure Key Vault**:

```csharp
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;

public class AzureSqlConnectionFactory : IConnectionFactory
{
    private readonly SecretClient _secretClient;
    private readonly string _secretName;
    private string? _cachedConnectionString;
    private DateTime _cacheExpiry;

    public AzureSqlConnectionFactory(string keyVaultUrl, string secretName)
    {
        _secretClient = new SecretClient(
            new Uri(keyVaultUrl),
            new DefaultAzureCredential()
        );
        _secretName = secretName;
    }

    public async Task<DbConnection> CreateConnectionAsync(CancellationToken ct)
    {
        // Refresh connection string every 5 minutes
        if (_cachedConnectionString == null || DateTime.UtcNow > _cacheExpiry)
        {
            var secret = await _secretClient.GetSecretAsync(_secretName, cancellationToken: ct);
            _cachedConnectionString = secret.Value.Value;
            _cacheExpiry = DateTime.UtcNow.AddMinutes(5);
        }

        var connection = new SqlConnection(_cachedConnectionString);
        await connection.OpenAsync(ct);
        return connection;
    }
}
```

### Azure Container Apps

Azure Container Apps is a serverless container platform.

#### Configuration (containerapp.yaml)

```yaml
apiVersion: apps.containerapp.azure.com/v1
kind: ContainerApp
metadata:
  name: batch-ingestor
spec:
  configuration:
    ingress:
      external: true
      targetPort: 80
      transport: http
    secrets:
      - name: db-connection-string
        keyVaultUrl: https://myvault.vault.azure.net/secrets/db-connection
    registries:
      - server: myregistry.azurecr.io
        identity: system
  template:
    containers:
      - image: myregistry.azurecr.io/batch-ingestor:latest
        name: batch-ingestor
        resources:
          cpu: 1.0
          memory: 2Gi
        env:
          - name: ASPNETCORE_ENVIRONMENT
            value: Production
          - name: ConnectionStrings__Database
            secretRef: db-connection-string
          - name: BatchIngestor__MaxCpuPercent
            value: "80"
        probes:
          - type: Liveness
            httpGet:
              path: /health
              port: 80
            initialDelaySeconds: 30
            periodSeconds: 30
          - type: Readiness
            httpGet:
              path: /health/ready
              port: 80
            initialDelaySeconds: 10
            periodSeconds: 10
    scale:
      minReplicas: 1
      maxReplicas: 10
      rules:
        - name: cpu-scaling
          custom:
            type: cpu
            metadata:
              type: Utilization
              value: "70"
        - name: memory-scaling
          custom:
            type: memory
            metadata:
              type: Utilization
              value: "80"
```

### Azure Functions (Serverless)

```csharp
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

public class BatchIngestorFunction
{
    private readonly IBatchIngestor<EventData> _ingestor;
    private readonly ILogger _logger;

    public BatchIngestorFunction(
        IBatchIngestor<EventData> ingestor,
        ILoggerFactory loggerFactory)
    {
        _ingestor = ingestor;
        _logger = loggerFactory.CreateLogger<BatchIngestorFunction>();
    }

    [Function("ProcessEventBatch")]
    public async Task Run(
        [ServiceBusTrigger("events-queue", Connection = "ServiceBusConnection")]
        EventData[] events,
        FunctionContext context)
    {
        _logger.LogInformation($"Processing {events.Length} events");

        var metrics = await _ingestor.IngestAsync(events, "events");

        _logger.LogInformation(
            $"Ingested {metrics.TotalRowsProcessed} rows, " +
            $"throughput: {metrics.RowsPerSecond:N0} rows/sec");
    }
}

// Startup configuration
public class Program
{
    public static void Main()
    {
        var host = new HostBuilder()
            .ConfigureFunctionsWorkerDefaults()
            .ConfigureServices(services =>
            {
                var connectionString = Environment.GetEnvironmentVariable("SqlConnectionString");

                services.AddSingleton<IConnectionFactory>(
                    new SimpleConnectionFactory(
                        connectionString,
                        () => new SqlConnection(connectionString)
                    ));

                services.AddSingleton<ISqlDialect, AzureSqlDialect>();

                services.AddSingleton<IRowMapper<EventData>, EventDataMapper>();

                services.AddSingleton(new BatchIngestOptions
                {
                    BatchSize = 1000,
                    MaxDegreeOfParallelism = 2,
                    MaxInFlightBatches = 5,
                    EnablePerformanceMetrics = false
                });

                services.AddSingleton(typeof(IBatchIngestor<>), typeof(BatchIngestor<>));
            })
            .Build();

        host.Run();
    }
}
```

## Google Cloud Platform (GCP)

### Cloud SQL for PostgreSQL

```csharp
using Npgsql;
using Tika.BatchIngestor.Dialects;

// Connection string (use Secret Manager in production)
var connectionString = "Host=/cloudsql/project:region:instance;" +  // Unix socket
                       "Database=mydb;" +
                       "Username=postgres;" +
                       "Password=***;" +
                       "Pooling=true;" +
                       "Maximum Pool Size=20";

// Or use public IP with Cloud SQL Proxy
// var connectionString = "Host=127.0.0.1;Port=5432;Database=mydb;...";

var connectionFactory = new SimpleConnectionFactory(
    connectionString,
    () => new NpgsqlConnection(connectionString)
);

var options = new BatchIngestOptions
{
    BatchSize = 4000,
    MaxDegreeOfParallelism = 4,
    MaxInFlightBatches = 12,
    CommandTimeoutSeconds = 600,
    EnableCpuThrottling = true,
    MaxCpuPercent = 80.0
};

var ingestor = new BatchIngestor<MyData>(
    connectionFactory,
    new GenericSqlDialect(),        // Use generic for Cloud SQL
    mapper,
    options
);
```

### Google Kubernetes Engine (GKE)

See [Kubernetes Deployment](#kubernetes-deployment) section below.

### Cloud Run (Serverless Containers)

```yaml
# service.yaml
apiVersion: serving.knative.dev/v1
kind: Service
metadata:
  name: batch-ingestor
spec:
  template:
    metadata:
      annotations:
        autoscaling.knative.dev/minScale: "1"
        autoscaling.knative.dev/maxScale: "10"
        autoscaling.knative.dev/target: "70"
    spec:
      containers:
        - image: gcr.io/project-id/batch-ingestor:latest
          ports:
            - containerPort: 8080
          env:
            - name: ASPNETCORE_ENVIRONMENT
              value: Production
            - name: DB_CONNECTION_STRING
              valueFrom:
                secretKeyRef:
                  name: db-connection
                  key: connection-string
          resources:
            limits:
              cpu: "2"
              memory: 2Gi
          livenessProbe:
            httpGet:
              path: /health
              port: 8080
            initialDelaySeconds: 30
            periodSeconds: 30
```

## Cloud-Agnostic Best Practices

### 1. Configuration Management

Use environment-based configuration:

```csharp
// appsettings.json (defaults)
{
  "BatchIngestor": {
    "BatchSize": 1000,
    "MaxDegreeOfParallelism": 4
  }
}

// appsettings.Production.json (cloud overrides)
{
  "BatchIngestor": {
    "BatchSize": 5000,
    "MaxDegreeOfParallelism": 6,
    "EnableCpuThrottling": true
  }
}

// Environment variables (runtime overrides)
// BatchIngestor__BatchSize=3000
// BatchIngestor__MaxCpuPercent=75
```

**Loading configuration**:

```csharp
public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        // Bind configuration
        var batchOptions = Configuration
            .GetSection("BatchIngestor")
            .Get<BatchIngestOptions>();

        services.AddSingleton(batchOptions);

        // Or use Options pattern
        services.Configure<BatchIngestOptions>(
            Configuration.GetSection("BatchIngestor"));
    }
}
```

### 2. Secret Management

Never hardcode credentials:

```csharp
// ❌ BAD: Hardcoded
var connectionString = "Server=...;Password=MyPassword123";

// ✅ GOOD: Environment variable
var connectionString = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING");

// ✅ BETTER: Secret manager
var connectionString = await secretManager.GetSecretAsync("db-connection");

// ✅ BEST: Managed identity with no password
var connectionString = "Server=...;Authentication=Active Directory Managed Identity";
```

### 3. Health Checks

Implement comprehensive health checks:

```csharp
public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddBatchIngestorHealthCheck("batch-ingestor")
            .AddNpgSql(connectionString, name: "database")
            .AddCheck("memory", () =>
            {
                var allocated = GC.GetTotalMemory(false);
                var threshold = 1_000_000_000; // 1 GB
                return allocated < threshold
                    ? HealthCheckResult.Healthy()
                    : HealthCheckResult.Degraded($"High memory: {allocated / 1_000_000} MB");
            });
    }

    public void Configure(IApplicationBuilder app)
    {
        app.UseHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => true,
            ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
        });

        app.UseHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready"),
            ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
        });
    }
}
```

### 4. Observability

Structured logging and metrics:

```csharp
var options = new BatchIngestOptions
{
    // ... config ...

    Logger = loggerFactory.CreateLogger<BatchIngestor>(),

    OnProgress = metrics =>
    {
        // Structured logging
        logger.LogInformation(
            "Batch ingestion progress: {RowsProcessed} rows, {Throughput} rows/sec, {CpuPercent}% CPU",
            metrics.TotalRowsProcessed,
            metrics.RowsPerSecond,
            metrics.CurrentPerformance?.CpuUsagePercent);

        // Export to monitoring system
        metricsCollector.RecordGauge("batch_ingestor.rows_per_second", metrics.RowsPerSecond);
        metricsCollector.RecordCounter("batch_ingestor.rows_processed", metrics.TotalRowsProcessed);
        metricsCollector.RecordGauge("batch_ingestor.cpu_percent",
            metrics.CurrentPerformance?.CpuUsagePercent ?? 0);
    }
};
```

## Container Deployment

### Dockerfile

```dockerfile
# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy and restore
COPY ["MyApp/MyApp.csproj", "MyApp/"]
RUN dotnet restore "MyApp/MyApp.csproj"

# Build
COPY . .
WORKDIR "/src/MyApp"
RUN dotnet build "MyApp.csproj" -c Release -o /app/build

# Publish
FROM build AS publish
RUN dotnet publish "MyApp.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Non-root user for security
RUN useradd -m -u 1000 appuser && chown -R appuser /app
USER appuser

COPY --from=publish /app/publish .

# Health check
HEALTHCHECK --interval=30s --timeout=5s --start-period=60s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

EXPOSE 8080
ENTRYPOINT ["dotnet", "MyApp.dll"]
```

### Docker Compose (Development)

```yaml
version: '3.8'

services:
  app:
    build: .
    ports:
      - "8080:8080"
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - ConnectionStrings__Database=Host=postgres;Database=mydb;Username=postgres;Password=postgres
      - BatchIngestor__BatchSize=2000
      - BatchIngestor__MaxDegreeOfParallelism=4
    depends_on:
      postgres:
        condition: service_healthy
    networks:
      - app-network

  postgres:
    image: postgres:15-alpine
    environment:
      - POSTGRES_DB=mydb
      - POSTGRES_USER=postgres
      - POSTGRES_PASSWORD=postgres
    ports:
      - "5432:5432"
    volumes:
      - postgres-data:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U postgres"]
      interval: 10s
      timeout: 5s
      retries: 5
    networks:
      - app-network

volumes:
  postgres-data:

networks:
  app-network:
    driver: bridge
```

## Kubernetes Deployment

### Deployment Manifest

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: batch-ingestor
  namespace: production
  labels:
    app: batch-ingestor
spec:
  replicas: 3
  selector:
    matchLabels:
      app: batch-ingestor
  template:
    metadata:
      labels:
        app: batch-ingestor
      annotations:
        prometheus.io/scrape: "true"
        prometheus.io/port: "8080"
        prometheus.io/path: "/metrics"
    spec:
      serviceAccountName: batch-ingestor
      containers:
        - name: batch-ingestor
          image: myregistry.azurecr.io/batch-ingestor:v1.0.0
          imagePullPolicy: Always
          ports:
            - containerPort: 8080
              name: http
              protocol: TCP
          env:
            - name: ASPNETCORE_ENVIRONMENT
              value: "Production"
            - name: ConnectionStrings__Database
              valueFrom:
                secretKeyRef:
                  name: db-connection
                  key: connection-string
            - name: BatchIngestor__BatchSize
              valueFrom:
                configMapKeyRef:
                  name: batch-ingestor-config
                  key: batch-size
            - name: BatchIngestor__MaxDegreeOfParallelism
              valueFrom:
                configMapKeyRef:
                  name: batch-ingestor-config
                  key: max-degree-of-parallelism
          resources:
            requests:
              cpu: 500m
              memory: 512Mi
            limits:
              cpu: 2000m
              memory: 2Gi
          livenessProbe:
            httpGet:
              path: /health/live
              port: 8080
            initialDelaySeconds: 30
            periodSeconds: 30
            timeoutSeconds: 5
            failureThreshold: 3
          readinessProbe:
            httpGet:
              path: /health/ready
              port: 8080
            initialDelaySeconds: 10
            periodSeconds: 10
            timeoutSeconds: 5
            failureThreshold: 3
          securityContext:
            runAsNonRoot: true
            runAsUser: 1000
            allowPrivilegeEscalation: false
            readOnlyRootFilesystem: true
            capabilities:
              drop:
                - ALL
---
apiVersion: v1
kind: Service
metadata:
  name: batch-ingestor
  namespace: production
spec:
  selector:
    app: batch-ingestor
  ports:
    - protocol: TCP
      port: 80
      targetPort: 8080
  type: ClusterIP
---
apiVersion: v1
kind: ConfigMap
metadata:
  name: batch-ingestor-config
  namespace: production
data:
  batch-size: "5000"
  max-degree-of-parallelism: "6"
  max-cpu-percent: "80"
---
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: batch-ingestor-hpa
  namespace: production
spec:
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: batch-ingestor
  minReplicas: 2
  maxReplicas: 10
  metrics:
    - type: Resource
      resource:
        name: cpu
        target:
          type: Utilization
          averageUtilization: 70
    - type: Resource
      resource:
        name: memory
        target:
          type: Utilization
          averageUtilization: 80
```

## Serverless Deployment

### Considerations for Serverless

1. **Cold Start**: Initialize `BatchIngestor` once and reuse
2. **Concurrency**: Control with function-level concurrency settings
3. **Timeout**: Ensure function timeout > batch processing time
4. **Memory**: More memory = more vCPU = better performance

### Configuration for Serverless

```csharp
var options = new BatchIngestOptions
{
    BatchSize = 1000,              // Smaller batches for quick completion
    MaxDegreeOfParallelism = 2,    // Limited vCPU in serverless
    MaxInFlightBatches = 5,        // Limited memory
    EnableCpuThrottling = false,   // Platform already handles
    EnablePerformanceMetrics = false,  // Reduce overhead
    CommandTimeoutSeconds = 300
};
```

## Monitoring and Observability

### Application Insights (Azure)

```csharp
public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddApplicationInsightsTelemetry();

        var options = new BatchIngestOptions
        {
            // ... config ...

            OnProgress = metrics =>
            {
                var telemetry = services.GetRequiredService<TelemetryClient>();

                telemetry.TrackMetric("BatchIngestor.RowsPerSecond", metrics.RowsPerSecond);
                telemetry.TrackMetric("BatchIngestor.TotalRows", metrics.TotalRowsProcessed);
                telemetry.TrackMetric("BatchIngestor.CpuPercent",
                    metrics.CurrentPerformance?.CpuUsagePercent ?? 0);
            }
        };
    }
}
```

### CloudWatch (AWS)

```csharp
using Amazon.CloudWatch;
using Amazon.CloudWatch.Model;

var cloudWatch = new AmazonCloudWatchClient();

var options = new BatchIngestOptions
{
    OnProgress = async metrics =>
    {
        await cloudWatch.PutMetricDataAsync(new PutMetricDataRequest
        {
            Namespace = "BatchIngestor",
            MetricData = new List<MetricDatum>
            {
                new MetricDatum
                {
                    MetricName = "RowsPerSecond",
                    Value = metrics.RowsPerSecond,
                    Unit = StandardUnit.Count,
                    TimestampUtc = DateTime.UtcNow
                },
                new MetricDatum
                {
                    MetricName = "CpuPercent",
                    Value = metrics.CurrentPerformance?.CpuUsagePercent ?? 0,
                    Unit = StandardUnit.Percent,
                    TimestampUtc = DateTime.UtcNow
                }
            }
        });
    }
};
```

### Cloud Monitoring (GCP)

```csharp
using Google.Cloud.Monitoring.V3;

var metricServiceClient = MetricServiceClient.Create();
var projectName = ProjectName.FromProject("my-project-id");

var options = new BatchIngestOptions
{
    OnProgress = metrics =>
    {
        var timeSeries = new TimeSeries
        {
            Metric = new Metric
            {
                Type = "custom.googleapis.com/batch_ingestor/rows_per_second"
            },
            Resource = new MonitoredResource
            {
                Type = "generic_task"
            },
            Points =
            {
                new Point
                {
                    Value = new TypedValue { DoubleValue = metrics.RowsPerSecond },
                    Interval = new TimeInterval
                    {
                        EndTime = Timestamp.FromDateTime(DateTime.UtcNow)
                    }
                }
            }
        };

        metricServiceClient.CreateTimeSeries(projectName, new[] { timeSeries });
    }
};
```

## Cost Optimization

### 1. Right-Size Resources

- Start with smaller instance types
- Monitor CPU and memory usage
- Scale up only when needed

### 2. Use Spot/Preemptible Instances

For non-critical batch jobs:
- AWS: EC2 Spot Instances (up to 90% savings)
- Azure: Spot VMs (up to 90% savings)
- GCP: Preemptible VMs (up to 80% savings)

### 3. Optimize Database Tier

- Use appropriate database tier for workload
- Consider reserved capacity for predictable workloads
- Use serverless/auto-pause for intermittent workloads

### 4. Batch Processing Windows

Schedule bulk ingestion during off-peak hours:
```csharp
// Use cheaper resources during off-peak
var options = new BatchIngestOptions
{
    BatchSize = IsOffPeakHours() ? 10000 : 2000,
    MaxDegreeOfParallelism = IsOffPeakHours() ? 8 : 2
};
```

### 5. Connection Pooling

Minimize connection overhead:
```csharp
// Connection string with pooling
"Pooling=true;Min Pool Size=4;Max Pool Size=20"
```

## Deployment Checklist

- [ ] Secrets stored in cloud secret manager (not in code or config)
- [ ] Connection strings use managed identities where possible
- [ ] Health checks configured and tested
- [ ] Resource limits set (CPU, memory)
- [ ] Monitoring and alerting configured
- [ ] Logging configured with appropriate levels
- [ ] Auto-scaling configured (if applicable)
- [ ] Backup and disaster recovery plan
- [ ] Performance tested under expected load
- [ ] Security best practices followed (non-root user, minimal permissions)
- [ ] Cost monitoring enabled
- [ ] Documentation updated with deployment specifics

## References

- [Architecture Overview](architecture.md)
- [Performance Tuning Guide](performance-tuning.md)
- [Health Check Integration](health-checks.md)
- [Troubleshooting](troubleshooting.md)
