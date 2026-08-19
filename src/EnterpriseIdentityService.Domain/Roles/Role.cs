using EnterpriseIdentityService.Domain.Abstractions;

namespace EnterpriseIdentityService.Domain.Roles;

public sealed class Role : AggregateRoot<RoleId>
{
    public const int MaximumNameLength = 100;

    private readonly List<RolePermission> _permissions = [];

    private Role(
        RoleId id,
        string name,
        string normalizedName,
        bool isSystem,
        bool isEnabled)
        : base(id)
    {
        Name = name;
        NormalizedName = normalizedName;
        IsSystem = isSystem;
        IsEnabled = isEnabled;
    }

    public string Name { get; private set; }

    public string NormalizedName { get; private set; }

    public bool IsSystem { get; }

    public bool IsEnabled { get; private set; }

    public int Version { get; private set; }

    public IReadOnlyCollection<RolePermission> Permissions => _permissions.AsReadOnly();

    public static Role Create(RoleId id, string name)
    {
        (string displayName, string normalizedName) = ValidateAndNormalizeName(name);
        return new Role(id, displayName, normalizedName, false, true);
    }

    public static Role CreateSystem(
        RoleId id,
        string name,
        IEnumerable<string> permissions)
    {
        ArgumentNullException.ThrowIfNull(permissions);
        (string displayName, string normalizedName) = ValidateAndNormalizeName(name);
        var role = new Role(id, displayName, normalizedName, true, true);
        role._permissions.AddRange(permissions
            .Distinct(StringComparer.Ordinal)
            .Select(permission => RolePermission.Create(id, permission)));
        return role;
    }

    public static string NormalizeName(string name) => ValidateAndNormalizeName(name).NormalizedName;

    public void Rename(string name)
    {
        EnsureMutable();
        (string displayName, string normalizedName) = ValidateAndNormalizeName(name);

        if (Name == displayName && NormalizedName == normalizedName)
        {
            return;
        }

        Name = displayName;
        NormalizedName = normalizedName;
        IncrementVersion();
    }

    public void Enable()
    {
        if (IsEnabled)
        {
            return;
        }

        IsEnabled = true;
        IncrementVersion();
    }

    public void Disable()
    {
        EnsureMutable();

        if (!IsEnabled)
        {
            return;
        }

        IsEnabled = false;
        IncrementVersion();
    }

    public void ReplacePermissions(IEnumerable<string> permissions)
    {
        EnsureMutable();
        ArgumentNullException.ThrowIfNull(permissions);

        string[] requested = permissions
            .Select(permission => string.IsNullOrWhiteSpace(permission)
                ? throw new ArgumentException("Permission identifiers cannot be empty.", nameof(permissions))
                : permission.Trim())
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        string[] current = _permissions
            .Select(rolePermission => rolePermission.Permission)
            .Order(StringComparer.Ordinal)
            .ToArray();

        if (current.SequenceEqual(requested, StringComparer.Ordinal))
        {
            return;
        }

        _permissions.RemoveAll(rolePermission =>
            !requested.Contains(rolePermission.Permission, StringComparer.Ordinal));
        HashSet<string> retained = _permissions
            .Select(rolePermission => rolePermission.Permission)
            .ToHashSet(StringComparer.Ordinal);
        _permissions.AddRange(requested
            .Where(permission => !retained.Contains(permission))
            .Select(permission => RolePermission.Create(Id, permission)));
        IncrementVersion();
    }

    public void EnsureCanDelete() => EnsureMutable();

    public void RecordAssignmentChange() => IncrementVersion();

    private void EnsureMutable()
    {
        if (IsSystem)
        {
            throw new InvalidOperationException("System roles cannot be modified.");
        }
    }

    private static (string DisplayName, string NormalizedName) ValidateAndNormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("A role name is required.", nameof(name));
        }

        string displayName = name.Trim();
        if (displayName.Length > MaximumNameLength)
        {
            throw new ArgumentException(
                $"A role name cannot exceed {MaximumNameLength} characters.", nameof(name));
        }

        return (displayName, displayName.ToUpperInvariant());
    }

    private void IncrementVersion() => Version = checked(Version + 1);
}
