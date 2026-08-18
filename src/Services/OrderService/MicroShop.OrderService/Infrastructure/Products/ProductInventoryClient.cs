using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace MicroShop.OrderService.Infrastructure.Products;

public sealed partial class ProductInventoryClient(
    HttpClient httpClient,
    IOptions<ProductServiceOptions> options,
    ILogger<ProductInventoryClient> logger) : IProductInventoryClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ProductServiceOptions serviceOptions = options.Value;

    public async Task<ProductReservationResult> ReserveAsync(
        ProductReservationRequest request,
        CancellationToken cancellationToken)
    {
        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Post,
            "internal/v1/inventory/reservations")
        {
            Content = JsonContent.Create(
                new
                {
                    orderId = request.OrderId,
                    items = request.Items.Select(item => new
                    {
                        productId = item.ProductId,
                        quantity = item.Quantity
                    })
                },
                options: JsonOptions)
        };
        AddTraceParent(httpRequest, request.TraceParent);

        using var timeout = new CancellationTokenSource(serviceOptions.Timeout);
        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeout.Token);

        try
        {
            using var response = await httpClient.SendAsync(
                httpRequest,
                HttpCompletionOption.ResponseHeadersRead,
                linkedCancellation.Token);
            var body = await response.Content.ReadAsStringAsync(linkedCancellation.Token);
            if (response.IsSuccessStatusCode)
            {
                return ParseReservationResponse(request.OrderId, response.StatusCode, body);
            }

            return MapReservationProblem(response.StatusCode, body);
        }
        catch (OperationCanceledException) when (
            !cancellationToken.IsCancellationRequested
            && timeout.IsCancellationRequested)
        {
            ReservationTimedOut(request.OrderId);
            return new ProductReservationResult(
                ProductReservationFailure.OutcomeUnknown,
                null,
                [],
                0,
                null,
                null,
                "The Product reservation outcome could not be determined before the timeout.",
                false,
                false);
        }
        catch (HttpRequestException exception)
        {
            ReservationUnavailable(exception, request.OrderId);
            return new ProductReservationResult(
                ProductReservationFailure.DependencyUnavailable,
                null,
                [],
                0,
                null,
                null,
                "The Product Service is unavailable.",
                false,
                false);
        }
    }

    public async Task<ProductReleaseResult> ReleaseAsync(
        ProductReleaseRequest request,
        CancellationToken cancellationToken)
    {
        using var timeout = new CancellationTokenSource(serviceOptions.Timeout);
        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeout.Token);

        for (var attempt = 0; ; attempt++)
        {
            try
            {
                using var httpRequest = new HttpRequestMessage(
                    HttpMethod.Post,
                    $"internal/v1/inventory/reservations/{request.OrderId:D}/release");
                AddTraceParent(httpRequest, request.TraceParent);

                using var response = await httpClient.SendAsync(
                    httpRequest,
                    HttpCompletionOption.ResponseHeadersRead,
                    linkedCancellation.Token);
                var body = await response.Content.ReadAsStringAsync(linkedCancellation.Token);
                if (IsTransient(response.StatusCode)
                    && attempt < serviceOptions.SafeRetryCount)
                {
                    await DelayBeforeRetryAsync(
                        request.OrderId,
                        attempt + 1,
                        linkedCancellation.Token);
                    continue;
                }

                return response.IsSuccessStatusCode
                    ? ParseReleaseResponse(request.OrderId, body)
                    : MapReleaseProblem(response.StatusCode, body);
            }
            catch (OperationCanceledException) when (
                !cancellationToken.IsCancellationRequested
                && timeout.IsCancellationRequested)
            {
                ReleaseTimedOut(request.OrderId);
                return new ProductReleaseResult(
                    ProductReservationFailure.OutcomeUnknown,
                    null,
                    "The Product release outcome could not be determined before the timeout.",
                    false);
            }
            catch (HttpRequestException exception)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (timeout.IsCancellationRequested)
                {
                    ReleaseTimedOut(request.OrderId);
                    return new ProductReleaseResult(
                        ProductReservationFailure.OutcomeUnknown,
                        null,
                        "The Product release outcome could not be determined before the timeout.",
                        false);
                }

                if (attempt < serviceOptions.SafeRetryCount
                    && !linkedCancellation.IsCancellationRequested)
                {
                    try
                    {
                        await DelayBeforeRetryAsync(
                            request.OrderId,
                            attempt + 1,
                            linkedCancellation.Token);
                    }
                    catch (OperationCanceledException) when (
                        !cancellationToken.IsCancellationRequested
                        && timeout.IsCancellationRequested)
                    {
                        ReleaseTimedOut(request.OrderId);
                        return new ProductReleaseResult(
                            ProductReservationFailure.OutcomeUnknown,
                            null,
                            "The Product release outcome could not be determined before the timeout.",
                            false);
                    }

                    continue;
                }

                ReleaseUnavailable(exception, request.OrderId);
                return new ProductReleaseResult(
                    ProductReservationFailure.DependencyUnavailable,
                    null,
                    "The Product Service is unavailable.",
                    false);
            }
        }
    }

    public async Task<ProductReservationLookupResult> GetReservationByOrderAsync(
        ProductReservationLookupRequest request,
        CancellationToken cancellationToken)
    {
        using var timeout = new CancellationTokenSource(serviceOptions.Timeout);
        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeout.Token);

        for (var attempt = 0; ; attempt++)
        {
            try
            {
                using var httpRequest = new HttpRequestMessage(
                    HttpMethod.Get,
                    $"internal/v1/inventory/reservations/by-order/{request.OrderId:D}");
                AddTraceParent(httpRequest, request.TraceParent);

                using var response = await httpClient.SendAsync(
                    httpRequest,
                    HttpCompletionOption.ResponseHeadersRead,
                    linkedCancellation.Token);
                var body = await response.Content.ReadAsStringAsync(linkedCancellation.Token);
                if (IsTransient(response.StatusCode)
                    && attempt < serviceOptions.SafeRetryCount)
                {
                    await DelayBeforeRetryAsync(
                        request.OrderId,
                        attempt + 1,
                        linkedCancellation.Token);
                    continue;
                }

                return response.IsSuccessStatusCode
                    ? ParseReservationLookupResponse(request.OrderId, body)
                    : MapReservationLookupProblem(response.StatusCode, body);
            }
            catch (OperationCanceledException) when (
                !cancellationToken.IsCancellationRequested
                && timeout.IsCancellationRequested)
            {
                ReservationLookupTimedOut(request.OrderId);
                return new ProductReservationLookupResult(
                    ProductReservationFailure.OutcomeUnknown,
                    null,
                    null,
                    null,
                    [],
                    0,
                    null,
                    null,
                    "The Product reservation lookup outcome could not be determined before the timeout.");
            }
            catch (HttpRequestException exception)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (timeout.IsCancellationRequested)
                {
                    ReservationLookupTimedOut(request.OrderId);
                    return new ProductReservationLookupResult(
                        ProductReservationFailure.OutcomeUnknown,
                        null,
                        null,
                        null,
                        [],
                        0,
                        null,
                        null,
                        "The Product reservation lookup outcome could not be determined before the timeout.");
                }

                if (attempt < serviceOptions.SafeRetryCount
                    && !linkedCancellation.IsCancellationRequested)
                {
                    try
                    {
                        await DelayBeforeRetryAsync(
                            request.OrderId,
                            attempt + 1,
                            linkedCancellation.Token);
                    }
                    catch (OperationCanceledException) when (
                        !cancellationToken.IsCancellationRequested
                        && timeout.IsCancellationRequested)
                    {
                        ReservationLookupTimedOut(request.OrderId);
                        return new ProductReservationLookupResult(
                            ProductReservationFailure.OutcomeUnknown,
                            null,
                            null,
                            null,
                            [],
                            0,
                            null,
                            null,
                            "The Product reservation lookup outcome could not be determined before the timeout.");
                    }

                    continue;
                }

                ReservationLookupUnavailable(exception, request.OrderId);
                return new ProductReservationLookupResult(
                    ProductReservationFailure.DependencyUnavailable,
                    null,
                    null,
                    null,
                    [],
                    0,
                    null,
                    null,
                    "The Product Service is unavailable.");
            }
        }
    }

    private async Task DelayBeforeRetryAsync(
        Guid orderId,
        int attempt,
        CancellationToken cancellationToken)
    {
        IdempotentOperationRetrying(orderId, attempt);
        await Task.Delay(serviceOptions.SafeRetryDelay, cancellationToken);
    }

    private static bool IsTransient(HttpStatusCode statusCode)
    {
        return statusCode is HttpStatusCode.RequestTimeout
            or HttpStatusCode.TooManyRequests
            || (int)statusCode >= 500;
    }

    private static ProductReservationResult ParseReservationResponse(
        Guid requestedOrderId,
        HttpStatusCode statusCode,
        string body)
    {
        try
        {
            var response = JsonSerializer.Deserialize<ProductReservationResponseDto>(body, JsonOptions);
            if (response is null
                || response.OrderId != requestedOrderId
                || response.ReservationId == Guid.Empty
                || !string.Equals(response.Status, "reserved", StringComparison.Ordinal)
                || !string.Equals(response.Currency, "VND", StringComparison.Ordinal)
                || response.Items is null
                || response.Items.Count == 0
                || response.TotalAmount < 0)
            {
                return InvalidReservationResponse();
            }

            return new ProductReservationResult(
                ProductReservationFailure.None,
                response.ReservationId,
                response.Items
                    .Select(item => new ProductReservationSnapshot(
                        item.ProductId,
                        item.ProductName,
                        item.UnitPrice,
                        item.Quantity,
                        item.Subtotal))
                    .ToArray(),
                response.TotalAmount,
                null,
                null,
                null,
                statusCode == HttpStatusCode.Created,
                response.IdempotentReplay);
        }
        catch (JsonException)
        {
            return InvalidReservationResponse();
        }
    }

    private static ProductReleaseResult ParseReleaseResponse(Guid requestedOrderId, string body)
    {
        try
        {
            var response = JsonSerializer.Deserialize<ProductReleaseResponseDto>(body, JsonOptions);
            return response is null
                || response.OrderId != requestedOrderId
                || response.ReservationId == Guid.Empty
                || !string.Equals(response.Status, "released", StringComparison.Ordinal)
                ? InvalidReleaseResponse()
                : new ProductReleaseResult(
                    ProductReservationFailure.None,
                    response.ReservationId,
                    null,
                    response.IdempotentReplay);
        }
        catch (JsonException)
        {
            return InvalidReleaseResponse();
        }
    }

    private static ProductReservationLookupResult ParseReservationLookupResponse(
        Guid requestedOrderId,
        string body)
    {
        try
        {
            var response = JsonSerializer.Deserialize<ProductReservationQueryResponseDto>(body, JsonOptions);
            if (response is null
                || response.OrderId != requestedOrderId
                || response.ReservationId == Guid.Empty
                || response.Status is not ("reserved" or "released")
                || !string.Equals(response.Currency, "VND", StringComparison.Ordinal)
                || response.Items is null
                || response.Items.Count == 0
                || response.TotalAmount < 0)
            {
                return InvalidReservationLookupResponse();
            }

            return new ProductReservationLookupResult(
                ProductReservationFailure.None,
                response.ReservationId,
                response.Status,
                response.Currency,
                response.Items
                    .Select(item => new ProductReservationSnapshot(
                        item.ProductId,
                        item.ProductName,
                        item.UnitPrice,
                        item.Quantity,
                        item.Subtotal))
                    .ToArray(),
                response.TotalAmount,
                response.CreatedAtUtc,
                response.ReleasedAtUtc,
                null);
        }
        catch (JsonException)
        {
            return InvalidReservationLookupResponse();
        }
    }

    private static ProductReservationResult MapReservationProblem(
        HttpStatusCode statusCode,
        string body)
    {
        var problem = ReadProblem(body);
        var failure = problem.Code switch
        {
            "PRODUCT_NOT_FOUND" => ProductReservationFailure.ProductNotFound,
            "PRODUCT_INACTIVE" => ProductReservationFailure.ProductInactive,
            "INSUFFICIENT_STOCK" => ProductReservationFailure.InsufficientStock,
            "RESERVATION_REQUEST_MISMATCH" => ProductReservationFailure.RequestMismatch,
            "RESERVATION_STATE_CONFLICT" => ProductReservationFailure.ReservationStateConflict,
            _ when statusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.GatewayTimeout => ProductReservationFailure.OutcomeUnknown,
            _ when (int)statusCode >= 500 => ProductReservationFailure.DependencyUnavailable,
            _ => ProductReservationFailure.InvalidResponse
        };
        return new ProductReservationResult(
            failure,
            null,
            [],
            0,
            problem.ProductId,
            problem.AvailableStock,
            problem.Detail,
            false,
            false);
    }

    private static ProductReleaseResult MapReleaseProblem(HttpStatusCode statusCode, string body)
    {
        var problem = ReadProblem(body);
        var failure = problem.Code switch
        {
            "RESERVATION_NOT_FOUND" => ProductReservationFailure.ReservationNotFound,
            "RESERVATION_STATE_CONFLICT" => ProductReservationFailure.ReservationStateConflict,
            _ when statusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.GatewayTimeout => ProductReservationFailure.OutcomeUnknown,
            _ when (int)statusCode >= 500 => ProductReservationFailure.DependencyUnavailable,
            _ => ProductReservationFailure.InvalidResponse
        };
        return new ProductReleaseResult(failure, null, problem.Detail, false);
    }

    private static ProductReservationLookupResult MapReservationLookupProblem(
        HttpStatusCode statusCode,
        string body)
    {
        var problem = ReadProblem(body);
        var failure = problem.Code switch
        {
            "RESERVATION_NOT_FOUND" => ProductReservationFailure.ReservationNotFound,
            _ when statusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.GatewayTimeout => ProductReservationFailure.OutcomeUnknown,
            _ when (int)statusCode >= 500 => ProductReservationFailure.DependencyUnavailable,
            _ => ProductReservationFailure.InvalidResponse
        };
        return new ProductReservationLookupResult(
            failure,
            null,
            null,
            null,
            [],
            0,
            null,
            null,
            problem.Detail ?? "The Product reservation lookup did not return a usable outcome.");
    }

    private static ProblemResponse ReadProblem(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            var code = root.TryGetProperty("code", out var codeElement)
                ? codeElement.GetString()
                : null;
            var detail = root.TryGetProperty("detail", out var detailElement)
                ? detailElement.GetString()
                : null;
            Guid? productId = null;
            int? availableStock = null;
            if (root.TryGetProperty("errors", out var errors)
                && errors.ValueKind == JsonValueKind.Object)
            {
                if (errors.TryGetProperty("productId", out var productIdElement)
                    && TryReadFirstString(errors, "productId", out var productIdText)
                    && Guid.TryParse(productIdText, out var parsedProductId))
                {
                    productId = parsedProductId;
                }

                if (errors.TryGetProperty("availableStock", out var availableStockElement)
                    && TryReadFirstString(errors, "availableStock", out var availableStockText)
                    && int.TryParse(
                        availableStockText,
                        System.Globalization.NumberStyles.Integer,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out var parsedAvailableStock))
                {
                    availableStock = parsedAvailableStock;
                }
            }

            return new ProblemResponse(code, detail, productId, availableStock);
        }
        catch (JsonException)
        {
            return new ProblemResponse(null, null, null, null);
        }
    }

    private static bool TryReadFirstString(
        JsonElement errors,
        string propertyName,
        out string? value)
    {
        value = null;
        if (!errors.TryGetProperty(propertyName, out var property)
            || property.ValueKind != JsonValueKind.Array
            || property.GetArrayLength() == 0
            || property[0].ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = property[0].GetString();
        return value is not null;
    }

    private static ProductReservationResult InvalidReservationResponse()
    {
        return new ProductReservationResult(
            ProductReservationFailure.InvalidResponse,
            null,
            [],
            0,
            null,
            null,
            "The Product Service returned an invalid reservation response.",
            false,
            false);
    }

    private static ProductReleaseResult InvalidReleaseResponse()
    {
        return new ProductReleaseResult(
            ProductReservationFailure.InvalidResponse,
            null,
            "The Product Service returned an invalid release response.",
            false);
    }

    private static ProductReservationLookupResult InvalidReservationLookupResponse()
    {
        return new ProductReservationLookupResult(
            ProductReservationFailure.InvalidResponse,
            null,
            null,
            null,
            [],
            0,
            null,
            null,
            "The Product Service returned an invalid reservation lookup response.");
    }

    private static void AddTraceParent(HttpRequestMessage request, string? traceParent)
    {
        if (!string.IsNullOrWhiteSpace(traceParent))
        {
            request.Headers.TryAddWithoutValidation("traceparent", traceParent);
        }
    }

    [LoggerMessage(
        EventId = 3107,
        Level = LogLevel.Information,
        Message = "Retrying idempotent Product Service operation for order {OrderId}; attempt {Attempt}")]
    private partial void IdempotentOperationRetrying(Guid orderId, int attempt);

    private sealed record ProductReservationResponseDto(
        Guid ReservationId,
        Guid OrderId,
        string Status,
        string Currency,
        decimal TotalAmount,
        IReadOnlyList<ProductReservationItemResponseDto>? Items,
        bool IdempotentReplay);

    private sealed record ProductReservationItemResponseDto(
        Guid ProductId,
        string ProductName,
        decimal UnitPrice,
        int Quantity,
        decimal Subtotal);

    private sealed record ProductReleaseResponseDto(
        Guid OrderId,
        Guid ReservationId,
        string Status,
        bool IdempotentReplay,
        DateTimeOffset? ReleasedAtUtc);

    private sealed record ProductReservationQueryResponseDto(
        Guid ReservationId,
        Guid OrderId,
        string Status,
        string Currency,
        decimal TotalAmount,
        IReadOnlyList<ProductReservationItemResponseDto>? Items,
        DateTimeOffset CreatedAtUtc,
        DateTimeOffset? ReleasedAtUtc);

    private sealed record ProblemResponse(
        string? Code,
        string? Detail,
        Guid? ProductId,
        int? AvailableStock);

    [LoggerMessage(
        EventId = 3101,
        Level = LogLevel.Warning,
        Message = "Product reservation timed out for order {OrderId}; outcome is ambiguous")]
    private partial void ReservationTimedOut(Guid orderId);

    [LoggerMessage(
        EventId = 3102,
        Level = LogLevel.Warning,
        Message = "Product reservation dependency was unavailable for order {OrderId}")]
    private partial void ReservationUnavailable(Exception exception, Guid orderId);

    [LoggerMessage(
        EventId = 3103,
        Level = LogLevel.Warning,
        Message = "Product release timed out for order {OrderId}; outcome is ambiguous")]
    private partial void ReleaseTimedOut(Guid orderId);

    [LoggerMessage(
        EventId = 3104,
        Level = LogLevel.Warning,
        Message = "Product release dependency was unavailable for order {OrderId}")]
    private partial void ReleaseUnavailable(Exception exception, Guid orderId);

    [LoggerMessage(
        EventId = 3105,
        Level = LogLevel.Warning,
        Message = "Product reservation lookup timed out for order {OrderId}")]
    private partial void ReservationLookupTimedOut(Guid orderId);

    [LoggerMessage(
        EventId = 3106,
        Level = LogLevel.Warning,
        Message = "Product reservation lookup dependency was unavailable for order {OrderId}")]
    private partial void ReservationLookupUnavailable(Exception exception, Guid orderId);
}
