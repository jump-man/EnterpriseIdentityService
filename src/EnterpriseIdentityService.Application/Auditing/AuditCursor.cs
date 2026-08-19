using System.Globalization;
using System.Text;
using EnterpriseIdentityService.Domain.Auditing;

namespace EnterpriseIdentityService.Application.Auditing;

public sealed record AuditCursor(DateTimeOffset OccurredAtUtc, AuditEntryId Id)
{
    public string Encode()
    {
        string value = string.Create(
            CultureInfo.InvariantCulture,
            $"{OccurredAtUtc.UtcTicks}:{Id.Value:N}");
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(value))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    public static bool TryDecode(string? value, out AuditCursor? cursor)
    {
        cursor = null;
        if (string.IsNullOrWhiteSpace(value) || value.Length > 100)
        {
            return false;
        }

        try
        {
            string base64 = value.Replace('-', '+').Replace('_', '/');
            base64 = base64.PadRight(base64.Length + ((4 - base64.Length % 4) % 4), '=');
            string decoded = Encoding.UTF8.GetString(Convert.FromBase64String(base64));
            string[] parts = decoded.Split(':', StringSplitOptions.None);
            if (parts.Length != 2 ||
                !long.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out long ticks) ||
                !Guid.TryParseExact(parts[1], "N", out Guid id) ||
                id == Guid.Empty)
            {
                return false;
            }

            var occurredAtUtc = new DateTimeOffset(ticks, TimeSpan.Zero);
            cursor = new AuditCursor(occurredAtUtc, new AuditEntryId(id));
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }
}
