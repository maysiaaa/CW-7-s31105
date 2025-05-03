using System.Data;
using CW_7_s31105.Exceptions;
using CW_7_s31105.Models.DTOs;
using Microsoft.Data.SqlClient;

namespace CW_7_s31105.Services;

public interface IDbService
{
    public Task<IEnumerable<Trip_CountryGetDTO>> GetTripDetailsAsync();
    public Task<IEnumerable<Client_TripGetDTO>> GetClientTripsAsync(int? id);
    
    public Task<int> CreateClientAsync(ClientCreateDTO client);
    public Task<bool> RegisterClientForTripAsync(int clientId, int tripId);
    public Task<bool> UnregisterClientFromTripAsync(int clientId, int tripId);
}

public class DbService(IConfiguration config) : IDbService

{
    private async Task<SqlConnection> GetConnectionAsync()
    {
        var connection = new SqlConnection(config.GetConnectionString("Default-db"));
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        return connection;
    }

    //W ENDPOINT UZYWAJAC SELECT Z BAZY DANYCH WYCIAGAM INFORMACJE OWSYTZKICH WYCIECZKACH ORAZ LISCIE KRAJOW. W PĘTLI DODAJE KRAJE DO LISTY PO CZYM GDY ID WYCIECZKI SIE ZMINIA DODAJE WYCIECZKE ORAZ LISTE KRAJOW DO SLOWNIKA Z KTOREGO POZNIEJ ROBIE LISTE KTORA ZWRACAM
    public async Task<IEnumerable<Trip_CountryGetDTO>> GetTripDetailsAsync()
    {
        var tripsDict = new Dictionary<TripGetDTO, List<string>>();
        await using var connection = await GetConnectionAsync();
        
        //SELECT ŁACZY TRZY TABELE ZE SOBA I WYPISUJE WSZYTKO Z TABELI TRIP ORAZ NAZWE Z TABELI COUNTRY
        var sql = """
                  SELECT t.IdTrip,t.Name,t.Description,t.DateFrom,t.DateTo,t.MaxPeople,c.Name 
                  FROM Trip t
                  INNER JOIN Country_Trip ct ON ct.IdTrip = t.IdTrip 
                  INNER JOIN Country c ON ct.IdCountry = c.IdCountry
                  ORDER BY t.IdTrip;
                  """;
        

        await using var command = new SqlCommand(sql, connection);

        await using var reader = await command.ExecuteReaderAsync();
        int? currentTripId = null;
        TripGetDTO currentTrip = null;


        List<string> countries = null;
        while (await reader.ReadAsync())
        {
            int tripId = reader.GetInt32(0);

            if (currentTripId != tripId)
            {
                currentTrip = new TripGetDTO()
                {
                    IdTrip = tripId,
                    Name = reader.GetString(1),
                    Description = reader.GetString(2),
                    DateFrom = reader.GetDateTime(3),
                    DateTo = reader.GetDateTime(4),
                    MaxPeople = reader.GetInt32(5),
                };
                countries.Add(reader.GetString(6));
                tripsDict.Add(currentTrip, countries);
                countries = new List<string>();
                currentTripId = tripId;
            }
            else
            {
                countries.Add(reader.GetString(6));
            }

        }
        if (currentTrip != null)
        {
            tripsDict.Add(currentTrip, countries);
        }

        var result = tripsDict.Select(entry => new Trip_CountryGetDTO()
        {
            IdTrip = entry.Key.IdTrip,
            Name = entry.Key.Name,
            Description = entry.Key.Description,
            DateFrom = entry.Key.DateFrom,
            DateTo = entry.Key.DateTo,
            MaxPeople = entry.Key.MaxPeople,
            Countries = entry.Value
        }).ToList();

        await reader.CloseAsync();
        return result;
    }

    
    public async Task<IEnumerable<Client_TripGetDTO>> GetClientTripsAsync(int? id)
    {
        var list = new List<Client_TripGetDTO>();

        await using var connection = await GetConnectionAsync();

        //
        var sql = """
                  SELECT t.IdTrip, t.Name, t.Description, t.DateFrom, t.DateTo, t.MaxPeople, 
                         ct.RegisteredAt, ct.PaymentDate
                  FROM Client c
                  INNER JOIN Client_Trip ct ON c.IdClient = ct.IdClient
                  INNER JOIN Trip t ON ct.IdTrip = t.IdTrip
                  WHERE c.IdClient = @ClientId;
                  """;

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@ClientId", id);

        await using var reader = await command.ExecuteReaderAsync();
        
        if (!reader.HasRows)
            throw new NotFoundException("Klient nie istnieje lub nie ma wycieczek.");

        while (await reader.ReadAsync())
        {
            var tripDetails = new Client_TripGetDTO()
            {
                IdTrip = reader.GetInt32(0),
                Name = reader.GetString(1),
                Description = reader.GetString(2),
                DateFrom = reader.GetDateTime(3),
                DateTo = reader.GetDateTime(4),
                MaxPeople = reader.GetInt32(5),
                RegisteredAt = reader.GetInt32(6),
                PaymentDate = reader.GetDateTime(7)
            };
            list.Add(tripDetails);
        }

        return list;
    }

    // W ENDPOINCIE TWORZONY JEST NOWY KLIENT NA PODSTAWIE DANYCH Z DTO I ZWRACANY JEST JEGO ID
    public async Task<int> CreateClientAsync(ClientCreateDTO client)
    {

        await using var connection = await GetConnectionAsync();
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        
        // SQL DODAJE NOWEGO KLIENTA DO TABELI CLIENT I ZWRACA JEGO ID
        var sql =
            """INSERT INTO Client (FirstName, LastName, Email, Telephone, Pesel) VALUES (@FirstName, @LastName, @Email, @Telephone, @Pesel) SELECT SCOPE_IDENTITY()""";


        await using var command = new SqlCommand(sql, connection, (SqlTransaction)transaction);

        {
            command.Parameters.AddWithValue("@FirstName", client.FirstName);
            command.Parameters.AddWithValue("@LastName", client.Lastname);
            command.Parameters.AddWithValue("@Email", client.Email);
            command.Parameters.AddWithValue("@Telephone", client.Telephone);
            command.Parameters.AddWithValue("@Pesel", client.Pesel);

            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt32(result);
        }

    }

    // W ENDPOINCIE ZAPISUJE KLIENTA(ID PODANE JAKO PARAMETR) NA WYCIECZKE PODANA JAK PARAMETR
    public async Task<bool> RegisterClientForTripAsync(int clientId, int tripId)
    {
        await using var connection = await GetConnectionAsync();
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using var cmd = new SqlCommand
        {
            Connection = connection,
            Transaction = (SqlTransaction)transaction,
            // SQL SPRAWDZA CZY KLIENT I WYCIECZKA ISTNIEJA, CZY JEST MIEJSCE I CZY KLIENT NIE JEST JUZ ZAREJESTROWANY, POTEM DODAJE REJESTRACJE
            CommandText = @"
            IF NOT EXISTS (SELECT 1 FROM Client WHERE IdClient = @idClient)
                THROW 50000, 'Client not found', 1;
            IF NOT EXISTS (SELECT 1 FROM Trip WHERE IdTrip = @idTrip)
                THROW 50000, 'Trip not found', 1;
            IF EXISTS (
                SELECT 1 FROM Trip t
                WHERE t.IdTrip = @idTrip AND 
                (SELECT COUNT(*) FROM Client_Trip WHERE IdTrip = @idTrip) >= t.MaxPeople)
                THROW 50000, 'Trip is full', 1;
            IF EXISTS (SELECT 1 FROM Client_Trip WHERE IdClient = @idClient AND IdTrip = @idTrip)
                THROW 50000, 'Client already registered', 1;

            INSERT INTO Client_Trip (IdClient, IdTrip, RegisteredAt)
            VALUES (@idClient, @idTrip, GETDATE());"
        };

        cmd.Parameters.AddWithValue("@idClient", clientId);
        cmd.Parameters.AddWithValue("@idTrip", tripId);

        try
        {
            await cmd.ExecuteNonQueryAsync();
            await transaction.CommitAsync();
            return true;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    // W ENDPOINCIE KLIENT USUWA SIE Z KONKRETNEJ WYCIECZKI
    public async Task<bool> UnregisterClientFromTripAsync(int clientId, int tripId)
    {
        await using var connection = await GetConnectionAsync();
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using var cmd = new SqlCommand
        {
            Connection = connection,
            Transaction = (SqlTransaction)transaction,
            // SQL SPRAWDZA CZY KLIENT JEST ZAREJESTROWANY NA WYCIECZKE I USUWA REJESTRACJE
            CommandText = @"
            IF NOT EXISTS (
                SELECT 1 FROM Client_Trip 
                WHERE IdClient = @idClient AND IdTrip = @idTrip
            )
                THROW 50000, 'Registration not found', 1;

            DELETE FROM Client_Trip 
            WHERE IdClient = @idClient AND IdTrip = @idTrip;"
        };

        cmd.Parameters.AddWithValue("@idClient", clientId);
        cmd.Parameters.AddWithValue("@idTrip", tripId);

        try
        {
            await cmd.ExecuteNonQueryAsync();
            await transaction.CommitAsync();
            return true;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
        
    }
}

    
