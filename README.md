# Robokassa SDK для C# / .NET

SDK для серверной интеграции с Robokassa на .NET 8 и новее. Основной способ создания платежей - счета через Invoice API. Отдельные сервисы отвечают за уведомления, возвраты, холдирование, рекуррентные списания, фискализацию и SMS.

## Подключение

Добавьте ссылку на проект SDK в своем приложении:

```sh
dotnet add path/to/your-app.csproj reference src/Robokassa/Robokassa.csproj
```

Или соберите NuGet-пакет для локального использования:

```sh
dotnet pack src/Robokassa/Robokassa.csproj -c Release -o artifacts/packages
```

## Создание платежа

```csharp
using Robokassa;
using Robokassa.Models;

using var client = new RobokassaClient(new RobokassaOptions
{
    MerchantLogin = Environment.GetEnvironmentVariable("ROBOKASSA_LOGIN")!,
    TestPassword1 = Environment.GetEnvironmentVariable("ROBOKASSA_TEST_PASSWORD1"),
    TestPassword2 = Environment.GetEnvironmentVariable("ROBOKASSA_TEST_PASSWORD2"),
    IsTest = true,
    HashAlgorithm = RobokassaHashAlgorithm.Md5
});

var invoice = await client.Invoices.CreateAsync(new CreateInvoiceRequest
{
    InvId = 1001,
    OutSum = 100.00m,
    Description = "Заказ №1001",
    InvoiceItems =
    [
        new InvoiceItem
        {
            Name = "Услуга",
            Quantity = 1,
            Cost = 100.00m,
            Tax = "none",
            PaymentMethod = "full_payment",
            PaymentObject = "service"
        }
    ],
    AdditionalParameters = new InvoiceParameters
    {
        Email = "buyer@example.com",
        PaymentMethods = ["BankCard", "SBP"]
    }
});

Console.WriteLine(invoice.Url);
```

Сохраните `Id`, `InvId` и `Url` вместе с заказом. Покупателя направьте на `Url`.

Для каждого нового заказа используйте уникальный `InvId`. Если номер не передан, Robokassa назначит его автоматически. Поле `OutSum` можно не указывать, если сумма определяется по `InvoiceItems`. В `InvoiceItem.Cost` передается цена единицы товара или услуги.

Денежные значения используют `decimal`, номера счетов - `long`.

Если нужна только ссылка, вызовите `Invoices.CreatePaymentUrlAsync(request)`. Этот метод тоже создает счет. Выберите один из двух методов для одного заказа.

## Работа со счетами

Получение счета:

```csharp
var result = await client.Invoices.GetAsync(new GetInvoiceRequest
{
    InvId = 1001
});

Console.WriteLine(result.InvoiceInformation.InvoiceStatus);
```

Поиск по периоду, статусам и типам ссылок:

```csharp
var page = await client.Invoices.ListAsync(new InvoiceFilter
{
    CurrentPage = 1,
    PageSize = 20,
    DateFrom = DateTimeOffset.UtcNow.AddDays(-30),
    DateTo = DateTimeOffset.UtcNow,
    InvoiceStatuses = ["NotPaid", "Paid", "Expired"],
    InvoiceTypes = ["OneTime", "Reusable"]
});
```

Отправка счета покупателю:

```csharp
await client.Invoices.NotifyCustomerAsync(new NotifyCustomerRequest
{
    InvId = 1001,
    CustomerNotificationEmail = "buyer@example.com"
});
```

Деактивация ссылки:

```csharp
await client.Invoices.DeactivateAsync(new DeactivateInvoiceRequest
{
    InvId = 1001
});
```

`GetAsync` принимает один идентификатор: `Id` или `InvId`. У `DeactivateAsync` есть третий вариант - `EncodedId`, последний сегмент ссылки на оплату. В одном запросе указывайте только один идентификатор.

Основные статусы счета: `NotPaid`, `Paid`, `Expired`. При создании `InvoiceType` по умолчанию равен `OneTime`. Для многоразовой ссылки передайте `Reusable`.

## Параметры платежа

Дополнительные параметры задаются в `CreateInvoiceRequest.AdditionalParameters`:

| Сценарий | Параметр |
| --- | --- |
| Холдирование | `StepByStep = true` |
| Материнский рекуррентный платеж | `Recurring = true` |
| Оплата сохраненной картой | `Token = "op-key"` |
| Сплитование | `Split = "{...}"` |
| Доступные способы оплаты | `PaymentMethods = ["BankCard", "SBP"]` |
| Дополнительное уведомление | `ResultUrl2 = "https://shop.example/robokassa/result2"` |
| Отдельный чек после оплаты | `IsWithoutFreeSale = true` |

Значения `AdditionalParameters` SDK сериализует в строки. URL и email передавайте без предварительного кодирования.

`StepByStep=true`, `Recurring=true` и `Token` нельзя использовать вместе. Сплитование недоступно в тестовом режиме. Поле `Split` принимает JSON-строку; ее внутреннюю структуру нужно уточнить у Robokassa, поскольку OpenAPI не содержит схемы этого поля.

## Настройка клиента

| Параметр | Назначение |
| --- | --- |
| `MerchantLogin` | Логин магазина |
| `Password1` | Создание обычных счетов, управляющие методы Invoice API и проверка обычного SuccessURL |
| `Password2` | Проверка обычного ResultURL и запрос OpStateExt |
| `Password3` | Подпись запросов на возврат |
| `TestPassword1` | Создание тестовых счетов и проверка тестового SuccessURL |
| `TestPassword2` | Проверка тестового ResultURL |
| `IsTest` | Тестовый режим, по умолчанию `true` |
| `HashAlgorithm` | Алгоритм подписи магазина, по умолчанию `Md5` |
| `RefundHashAlgorithm` | Алгоритм подписи возвратов, по умолчанию `Sha256` |
| `Timeout` | Таймаут запроса, по умолчанию 15 секунд |
| `MaxResponseBytes` | Максимальный размер ответа, по умолчанию 4 МиБ |

В тестовом режиме SDK добавляет `AdditionalParameters.IsTest = "1"` и подписывает счет тестовым паролем №1. Для основного режима установите `IsTest = false` и задайте основные пароли магазина.

Пароли проверяются при вызове метода, которому они нужны. Для создания тестового счета достаточно `TestPassword1`. Управляющие методы Invoice API используют основной `Password1`, даже если клиент настроен на тестовое создание.

`HashAlgorithm` должен совпадать с алгоритмом в настройках магазина. Поддерживаются `Md5`, `Ripemd160`, `Sha1`, `Sha256`, `Sha384` и `Sha512`. Алгоритм применяется к JWT Invoice API и классическим подписям.

Для возвратов доступны `Sha256`, `Sha384` и `Sha512`, настройка задается через `RefundHashAlgorithm`.

Все сетевые методы принимают `CancellationToken`. Собственный `HttpClient` можно передать вторым аргументом конструктора:

```csharp
var client = new RobokassaClient(options, httpClient);
```

SDK не освобождает переданный `HttpClient`. При его настройке отключите автоматические переадресации и повторы денежных запросов. Базовые адреса можно заменить через `Endpoints.Services` и `Endpoints.Merchant`.

Пароли должны оставаться на сервере. Не записывайте JWT и подписанные платежные ссылки в логи.

## Обработка уведомлений

### ResultURL

```csharp
var notification = client.Notifications.VerifyResultUrl(
    fields,
    expectedAmount: order.Amount);

// атомарно сохраните оплату и обработайте повтор по notification.InvoiceId
var response = NotificationService.ResultResponse(notification);
```

`fields` имеет тип `IEnumerable<KeyValuePair<string, string>>`. Передавайте декодированные поля формы. Повторяющиеся ключи нужно сохранить при разборе запроса, чтобы SDK мог отклонить такое уведомление.

SDK проверяет подпись и, если задан `expectedAmount`, сумму заказа. Передавайте `OutSum` исходной строкой из уведомления, без изменения точности.

Ответ `OK{InvId}` отправляйте после записи оплаты в базе. Повторное уведомление должно возвращать тот же ответ без повторной выдачи товара или услуги.

Для проверки SuccessURL есть `VerifySuccessUrl`. Сам переход покупателя на эту страницу не заменяет серверное подтверждение оплаты.

### ResultUrl2

```csharp
var notification = client.Notifications.VerifyResultUrl2(
    compactJws,
    trustedCertificate);
```

Метод принимает компактную строку JWS, извлеченную из JSON-тела, и заранее полученный доверенный сертификат Robokassa. Сертификаты и адреса из заголовка самого JWS не используются.

По умолчанию ожидается `RS256`. Если используется другой алгоритм, укажите `expectedAlgorithm: "RS384"` или `"RS512"`. Параметр `maxAge` ограничивает возраст уведомления, `now` задает время для теста. Учет повторно полученных событий остается на стороне приложения.

## Другие операции

Для создания платежей используется Invoice API. Подтверждение и отмена холда, возвраты и дочерние рекуррентные списания выполняются отдельными запросами.

Подтвердить холд:

```csharp
await client.Holding.ConfirmAsync(invoiceId: 1001, outSum: "100.00");
```

Отменить холд:

```csharp
await client.Holding.CancelAsync(invoiceId: 1001, outSum: "100.00");
```

Успешный ответ означает принятие запроса. Окончательное состояние проверяется по уведомлению или через OpStateExt. При ошибке сначала запросите состояние: `20` - HOLD, `50` - обработка, `60` - отмена, `100` - операция завершена. Автоматически повторять Confirm или Cancel нельзя.

Создать возврат и проверить его состояние:

```csharp
var refund = await client.Refunds.CreateAsync(new CreateRefundRequest
{
    OpKey = "op-key"
});

var state = await client.Refunds.GetStateAsync(Guid.Parse(refund.RequestId!));
```

Без `RefundSum` создается полный возврат. Для частичного укажите сумму. API возвратов должно быть подключено магазину; для подписи нужен `Password3`.

Выполнить рекуррентное списание:

```csharp
await client.Recurring.CreateAsync(
    invoiceId: 1002,
    previousInvoiceId: 1001,
    outSum: "100.00");
```

`previousInvoiceId` - номер успешно оплаченного материнского платежа. Ответ `OK{InvId}` подтверждает создание операции, результат списания нужно получить отдельно.

Получить способы оплаты, состояние операции или отправить SMS:

```csharp
var methods = await client.Reference.GetCurrenciesAsync();
var operation = await client.Reference.GetOperationStateAsync(1001);
await client.Messaging.SendAsync("79990000000", "Сообщение");
```

OpStateExt не возвращает тестовые операции. Для работы со счетами используйте `Invoices.GetAsync`.

`Fiscalization.RegisterAsync` регистрирует чеки после оплаты через `/Fiscalization.Api.External/Receipt/Register`. Для запроса используются модели `RegisterFiscalReceiptRequest` и `DeferredFiscalReceipt`.

`ClassicPayments` создает подписанные URL и поля POST-форм для классического интерфейса, CoFPayment и CreateV2. Методы работают локально, без HTTP-запросов. При выводе полей в HTML используйте экранирование вашего веб-фреймворка.

Холды, создание возвратов, дочерние рекуррентные списания, SMS, регистрация чеков, OpStateExt и прямые CoF/CreateV2 недоступны при `IsTest = true`.

SDK не повторяет запросы автоматически. После таймаута сначала выясните результат предыдущего запроса, особенно при списании и возврате средств.

## Ошибки

```csharp
try
{
    await client.Invoices.CreateAsync(request);
}
catch (RobokassaException error)
{
    Console.WriteLine($"{error.Operation}: {error.Kind}, HTTP {error.StatusCode}");
}
```

В `RobokassaException` доступны `Kind`, `Operation`, `StatusCode` и `ApiCode`.

Прикладные отказы обрабатываются как ошибки даже при HTTP 200: `isSuccess=false`, `success=false`, отказ по отдельному чеку или `Result.Code != 0` в XML-ответе.

Отмена через `CancellationToken` сохраняет `OperationCanceledException`. Таймаут SDK возвращает `RobokassaException` с `Kind = Timeout`. Ошибки параметров запроса используют `ArgumentException` и `InvalidOperationException`.

## Пример ASP.NET Core

В `examples/Robokassa.AspNetCore` есть приложение с созданием счета и обработчиком ResultURL. Оно проверяет сумму заказа и обрабатывает повторные уведомления.

Задайте переменные окружения из `.env.example`, затем запустите приложение:

```sh
dotnet run --project examples/Robokassa.AspNetCore --urls http://localhost:5080
curl -X POST http://localhost:5080/orders
```

Файл `.env` автоматически не загружается. В настройках магазина укажите публичный HTTPS-адрес обработчика `/robokassa/result`.

В примере заказы хранятся в памяти, а маршрут создания заказа не требует авторизации. При интеграции замените `DemoOrderStore` хранилищем в базе данных и добавьте авторизацию. Запись оплаты и постановка заказа на выдачу должны выполняться атомарно.

## Сборка

Для сборки нужен .NET SDK 8 или новее:

```sh
dotnet build src/Robokassa/Robokassa.csproj -c Release
```

Запуск тестов на .NET 8:

```sh
dotnet test tests/Robokassa.Tests -c Release -p:TestTargetFramework=net8.0
```

Для проверки на .NET 10 замените `net8.0` на `net10.0`. Тесты не обращаются к платежному сервису.

## Лицензия

[MIT](LICENSE).
