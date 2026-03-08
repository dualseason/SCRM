using System;
using System.IO;
using Npgsql;

class Program
{
    static void Main()
    {
        string connStr = "Host=127.0.0.1;Port=5432;Database=SCRM;Username=postgres;Password=kiss1314;SSL Mode=Disable";
        try
        {
            using var conn = new NpgsqlConnection(connStr);
            conn.Open();
            using var cmd = new NpgsqlCommand("SELECT message_id, account_id, sender_wxid, receiver_wxid, message_type, content, direction FROM \"Messages\" ORDER BY created_at DESC LIMIT 10", conn);
            using var reader = cmd.ExecuteReader();
            Console.WriteLine("id | account | sender | receiver | type | dir | content");
            while (reader.Read())
            {
                var content = reader["content"].ToString();
                if (content.Length > 50) content = content.Substring(0, 50) + "...";
                Console.WriteLine($"{reader["message_id"]} | {reader["account_id"]} | {reader["sender_wxid"]} | {reader["receiver_wxid"]} | {reader["message_type"]} | {reader["direction"]} | {content}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error: " + ex.Message);
        }
    }
}
