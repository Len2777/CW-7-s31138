using System.Data;
using CW7APBD.Exeptions;
using CW7APBD.Models;
using CW7APBD.Models.Dtos;
using Microsoft.Data.SqlClient;

namespace CW7APBD.Services;

public interface IDbService
{
    public Task<IEnumerable<TripGetDTO>> GetTripsDetailsAsync();
    public Task<Client> AddClientAsync(ClientAdd clientData);
    public Task<List<ClientTripGetDto>> GetClientTripsAsync(int idClient);
    public Task RegisterClientForTripAsync(int idClient, int idTrip);
    public Task DeregisterClientFromTripAsync(int idClient, int idTrip);
}

public class DbService(IConfiguration config) : IDbService
{
    // Zwraca szczegóły wszystkich dostępnych wycieczek
    public async Task<IEnumerable<TripGetDTO>> GetTripsDetailsAsync()
    {
        await using var conn = await GetConnectionAsync();

        var tripCountries = new Dictionary<int, List<Country>>();
        const string countryQuery = """
           SELECT CT.IdTrip, C.IdCountry, C.Name 
           FROM Country C
           JOIN dbo.Country_Trip CT ON C.IdCountry = CT.IdCountry;
        """;

        await using (var cmd = new SqlCommand(countryQuery, conn))
        await using (var reader = await cmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                var tripId = reader.GetInt32(0);
                var country = new Country
                {
                    IdCountry = reader.GetInt32(1),
                    Name = reader.GetString(2)
                };

                if (!tripCountries.TryGetValue(tripId, out var list))
                    list = new List<Country>();

                list.Add(country);
                tripCountries[tripId] = list;
            }
        }

        const string tripsQuery = """
            SELECT IdTrip, Name, Description, DateFrom, DateTo, MaxPeople
            FROM Trip;
        """;

        var trips = new List<TripGetDTO>();

        await using (var cmd = new SqlCommand(tripsQuery, conn))
        await using (var reader = await cmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                var id = reader.GetInt32(0);
                trips.Add(new TripGetDTO
                {
                    TripId = id,
                    Name = reader.GetString(1),
                    Description = reader.GetString(2),
                    DateFrom = reader.GetDateTime(3),
                    DateTo = reader.GetDateTime(4),
                    MaxPeople = reader.GetInt32(5),
                    Countries = tripCountries.GetValueOrDefault(id) ?? new()
                });
            }
        }

        return trips;
    }
    // Dodaje nowego klienta do bazy danych
    public async Task<Client> AddClientAsync(ClientAdd data)
    {
        await using var conn = await GetConnectionAsync();
        const string sql = """
            INSERT INTO Client (FirstName, LastName, Email, Telephone, Pesel)
            VALUES (@FirstName, @LastName, @Email, @Telephone, @Pesel);
            SELECT SCOPE_IDENTITY();
        """;

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@FirstName", data.FirstName);
        cmd.Parameters.AddWithValue("@LastName", data.LastName);
        cmd.Parameters.AddWithValue("@Email", data.Email);
        cmd.Parameters.AddWithValue("@Telephone", data.Telephone);
        cmd.Parameters.AddWithValue("@Pesel", data.Pesel);

        var id = Convert.ToInt32(await cmd.ExecuteScalarAsync());

        return new Client
        {
            IdClient = id,
            FirstName = data.FirstName,
            LastName = data.LastName,
            Email = data.Email,
            Telephone = data.Telephone,
            Pesel = data.Pesel
        };
    }
    
    // Pobiera listę wycieczek, na które zapisany jest klient
    public async Task<List<ClientTripGetDto>> GetClientTripsAsync(int idClient)
    {
        if (!await CheckIfClientExistsAsync(idClient))
            throw new NotFoundException("Client does not exist");

        await using var conn = await GetConnectionAsync();
        const string sql = """
            SELECT
                T.IdTrip, T.Name, T.Description, T.DateFrom, T.DateTo,
                T.MaxPeople, CT.PaymentDate, CT.RegisteredAt
            FROM Trip T
            JOIN Client_Trip CT ON T.IdTrip = CT.IdTrip
            WHERE CT.IdClient = @IdClient;
        """;

        var trips = new List<ClientTripGetDto>();

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@IdClient", idClient);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            trips.Add(new ClientTripGetDto
            {
                IdTrip = reader.GetInt32(0),
                Name = reader.GetString(1),
                Description = reader.GetString(2),
                DateFrom = reader.GetDateTime(3),
                DateTo = reader.GetDateTime(4),
                MaxPeople = reader.GetInt32(5),
                PaymentDate = reader.IsDBNull(6) ? null : reader.GetInt32(6),
                RegisteredAt = reader.GetInt32(7)
            });
        }

        return trips;
    }

    // Rejestruje klienta na wycieczkę, jeśli są dostępne miejsca
    public async Task RegisterClientForTripAsync(int idClient, int idTrip)
    {
        if (!await CheckIfClientExistsAsync(idClient))
            throw new NotFoundException("Client not found");

        if (!await CheckIfTripExistsAsync(idTrip))
            throw new NotFoundException("Trip not found");

        await using var conn = await GetConnectionAsync();
        await using var tran = await conn.BeginTransactionAsync();

        try
        {
            const string checkCapacity = """
                SELECT 1 FROM Trip
                WHERE IdTrip = @IdTrip AND MaxPeople > (
                    SELECT COUNT(*) FROM Client_Trip WHERE IdTrip = @IdTrip
                );
            """;

            await using var checkCmd = new SqlCommand(checkCapacity, conn, (SqlTransaction)tran);
            checkCmd.Parameters.AddWithValue("@IdTrip", idTrip);

            if (await checkCmd.ExecuteScalarAsync() is null)
                throw new OutOfLimitExeption("Too many people");

            var date = DateTime.Now;
            var registeredAt = date.Year * 10000 + date.Month * 100 + date.Day;

            const string insertSql = """
                INSERT INTO Client_Trip (IdClient, IdTrip, RegisteredAt)
                VALUES (@IdClient, @IdTrip, @RegisteredAt);
            """;

            await using var insertCmd = new SqlCommand(insertSql, conn, (SqlTransaction)tran);
            insertCmd.Parameters.AddWithValue("@IdClient", idClient);
            insertCmd.Parameters.AddWithValue("@IdTrip", idTrip);
            insertCmd.Parameters.AddWithValue("@RegisteredAt", registeredAt);

            await insertCmd.ExecuteNonQueryAsync();
            await tran.CommitAsync();
        }
        catch
        {
            await tran.RollbackAsync();
            throw;
        }
    }

    // Anuluje rejestrację klienta z danej wycieczki
    public async Task DeregisterClientFromTripAsync(int idClient, int idTrip)
    {
        if (!await CheckIfClientExistsAsync(idClient))
            throw new NotFoundException("Client not found");

        if (!await CheckIfTripExistsAsync(idTrip))
            throw new NotFoundException("Trip not found");

        await using var conn = await GetConnectionAsync();
        await using var tran = await conn.BeginTransactionAsync();

        try
        {
            const string existsSql = """
                SELECT 1 FROM Client_Trip
                WHERE IdClient = @IdClient AND IdTrip = @IdTrip;
            """;

            await using var checkCmd = new SqlCommand(existsSql, conn, (SqlTransaction)tran);
            checkCmd.Parameters.AddWithValue("@IdClient", idClient);
            checkCmd.Parameters.AddWithValue("@IdTrip", idTrip);

            if (await checkCmd.ExecuteScalarAsync() is null)
                throw new NotFoundException("Registration not found");

            const string deleteSql = """
                DELETE FROM Client_Trip
                WHERE IdClient = @IdClient AND IdTrip = @IdTrip;
            """;

            await using var deleteCmd = new SqlCommand(deleteSql, conn, (SqlTransaction)tran);
            deleteCmd.Parameters.AddWithValue("@IdClient", idClient);
            deleteCmd.Parameters.AddWithValue("@IdTrip", idTrip);

            await deleteCmd.ExecuteNonQueryAsync();
            await tran.CommitAsync();
        }
        catch
        {
            await tran.RollbackAsync();
            throw;
        }
    }
    // Sprawdza, czy klient istnieje w bazie danych
    private async Task<bool> CheckIfClientExistsAsync(int idClient)
    {
        await using var conn = await GetConnectionAsync();
        const string sql = "SELECT 1 FROM Client WHERE IdClient = @IdClient;";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@IdClient", idClient);
        return await cmd.ExecuteScalarAsync() is not null;
    }
    
    // Sprawdza, czy wycieczka istnieje w bazie danych
    private async Task<bool> CheckIfTripExistsAsync(int idTrip)
    {
        await using var conn = await GetConnectionAsync();
        const string sql = "SELECT 1 FROM Trip WHERE IdTrip = @IdTrip;";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@IdTrip", idTrip);
        return await cmd.ExecuteScalarAsync() is not null;
    }

    // Tworzy i otwiera połączenie z bazą danych
    private async Task<SqlConnection> GetConnectionAsync()
    {
        var conn = new SqlConnection(config.GetConnectionString("Default"));
        if (conn.State != ConnectionState.Open)
            await conn.OpenAsync();
        return conn;
    }
}