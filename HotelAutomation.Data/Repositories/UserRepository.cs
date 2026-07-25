namespace HotelAutomation.Data.Repositories;

using Dapper;
using HotelAutomation.Core.Models;

public sealed class UserRepository
{
    private readonly SqliteConnectionFactory _factory;

    public UserRepository(SqliteConnectionFactory factory) => _factory = factory;

    public async Task<User?> GetByUsernameAsync(string username, CancellationToken ct = default)
    {
        await using var conn = await _factory.OpenAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<User>(
            "SELECT * FROM Users WHERE Username = @username",
            new { username });
    }

    public async Task<User?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        await using var conn = await _factory.OpenAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<User>(
            "SELECT * FROM Users WHERE Id = @id",
            new { id });
    }

    public async Task<IReadOnlyList<User>> GetAllAsync(CancellationToken ct = default)
    {
        await using var conn = await _factory.OpenAsync(ct);
        var results = await conn.QueryAsync<User>("SELECT * FROM Users ORDER BY CreatedAt DESC");
        return results.AsList();
    }

    public async Task AddAsync(User user, CancellationToken ct = default)
    {
        await using var conn = await _factory.OpenAsync(ct);
        await conn.ExecuteAsync(
            "INSERT INTO Users (Username, PasswordHash, Role, IsActive, CreatedAt) " +
            "VALUES (@Username, @PasswordHash, @Role, @IsActive, @CreatedAt)",
            new
            {
                user.Username,
                user.PasswordHash,
                user.Role,
                IsActive = user.IsActive ? 1 : 0,
                CreatedAt = user.CreatedAt.ToString("O")
            });
    }

    public async Task UpdateAsync(User user, CancellationToken ct = default)
    {
        await using var conn = await _factory.OpenAsync(ct);
        await conn.ExecuteAsync(
            "UPDATE Users SET Username = @Username, PasswordHash = @PasswordHash, " +
            "Role = @Role, IsActive = @IsActive WHERE Id = @Id",
            new
            {
                user.Username,
                user.PasswordHash,
                user.Role,
                IsActive = user.IsActive ? 1 : 0,
                user.Id
            });
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        await using var conn = await _factory.OpenAsync(ct);
        await conn.ExecuteAsync("DELETE FROM Users WHERE Id = @id", new { id });
    }

    public async Task SeedDefaultOwnerAsync(string defaultPasswordHash, string srkPasswordHash, CancellationToken ct = default)
    {
        await using var conn = await _factory.OpenAsync(ct);
        var count = await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Users");
        if (count == 0)
        {
            await conn.ExecuteAsync(
                "INSERT INTO Users (Username, PasswordHash, Role, IsActive, CreatedAt) " +
                "VALUES (@Username, @PasswordHash, @Role, @IsActive, @CreatedAt)",
                new
                {
                    Username = "admin",
                    PasswordHash = defaultPasswordHash,
                    Role = "Owner",
                    IsActive = 1,
                    CreatedAt = DateTimeOffset.Now.ToString("O")
                });
        }

        // Also ensure SRKadmin exists
        var srkCount = await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Users WHERE Username = 'SRKadmin'");
        if (srkCount == 0)
        {
            await conn.ExecuteAsync(
                "INSERT INTO Users (Username, PasswordHash, Role, IsActive, CreatedAt) " +
                "VALUES (@Username, @PasswordHash, @Role, @IsActive, @CreatedAt)",
                new
                {
                    Username = "SRKadmin",
                    PasswordHash = srkPasswordHash,
                    Role = "Developer",
                    IsActive = 1,
                    CreatedAt = DateTimeOffset.Now.ToString("O")
                });
        }

        // Seed default permissions if empty
        var permCount = await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM RolePermissions");
        if (permCount == 0)
        {
            var defaultPerms = new[]
            {
                // Manager permissions
                new { Role = "Manager", Module = "dashboard", IsEnabled = 1 },
                new { Role = "Manager", Module = "contacts", IsEnabled = 1 },
                new { Role = "Manager", Module = "templates", IsEnabled = 1 },
                new { Role = "Manager", Module = "broadcasts", IsEnabled = 1 },
                new { Role = "Manager", Module = "rules", IsEnabled = 1 },
                new { Role = "Manager", Module = "analytics", IsEnabled = 1 },
                new { Role = "Manager", Module = "optouts", IsEnabled = 1 },
                new { Role = "Manager", Module = "manual-checkin", IsEnabled = 1 },
                new { Role = "Manager", Module = "manual-checkout", IsEnabled = 1 },

                // Employer permissions
                new { Role = "Employer", Module = "dashboard", IsEnabled = 1 },
                new { Role = "Employer", Module = "contacts", IsEnabled = 1 },
                new { Role = "Employer", Module = "templates", IsEnabled = 1 },
                new { Role = "Employer", Module = "broadcasts", IsEnabled = 1 },
                new { Role = "Employer", Module = "rules", IsEnabled = 0 },
                new { Role = "Employer", Module = "analytics", IsEnabled = 1 },
                new { Role = "Employer", Module = "optouts", IsEnabled = 1 },
                new { Role = "Employer", Module = "manual-checkin", IsEnabled = 1 },
                new { Role = "Employer", Module = "manual-checkout", IsEnabled = 1 }
            };

            foreach (var perm in defaultPerms)
            {
                await conn.ExecuteAsync(
                    "INSERT INTO RolePermissions (Role, Module, IsEnabled) VALUES (@Role, @Module, @IsEnabled)",
                    perm);
            }
        }
    }

    public async Task<IEnumerable<dynamic>> GetPermissionsAsync(CancellationToken ct = default)
    {
        await using var conn = await _factory.OpenAsync(ct);
        return await conn.QueryAsync("SELECT Role, Module, IsEnabled FROM RolePermissions");
    }

    public async Task SavePermissionAsync(string role, string module, bool isEnabled, CancellationToken ct = default)
    {
        await using var conn = await _factory.OpenAsync(ct);
        await conn.ExecuteAsync(
            "INSERT INTO RolePermissions (Role, Module, IsEnabled) VALUES (@role, @module, @isEnabled) " +
            "ON CONFLICT(Role, Module) DO UPDATE SET IsEnabled = @isEnabled",
            new { role, module, isEnabled = isEnabled ? 1 : 0 });
    }
}
