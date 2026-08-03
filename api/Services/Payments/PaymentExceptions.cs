namespace api.Services.Payments;

/// <summary>İstemci hatası (400) olarak map'lenecek doğrulama hataları (ör. geçersiz PayoutAddress).</summary>
public class PaymentValidationException : Exception
{
    public string Code { get; }

    public PaymentValidationException(string code, string message) : base(message)
    {
        Code = code;
    }
}

/// <summary>404 olarak map'lenecek — invoice bulunamadı.</summary>
public class PaymentInvoiceNotFoundException : Exception
{
    public PaymentInvoiceNotFoundException(string invoiceId) : base($"Invoice bulunamadı: {invoiceId}")
    {
    }
}
