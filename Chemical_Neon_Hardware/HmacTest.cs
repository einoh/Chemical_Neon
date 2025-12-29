using System;
using System.Security.Cryptography;
using System.Text;

class HmacTest
{
    static void Main()
    {
        string SECRET_KEY = "b83f29aae116030da1bac6691471c8fa";
        string MACHINE_ID = "SITE_001";

        Console.WriteLine("Test 1: SITE_001:1:11");
        string msg1 = MACHINE_ID + ":1:11";
        string sig1 = ComputeHmac(msg1, SECRET_KEY);
        Console.WriteLine("Computed: " + sig1);
        Console.WriteLine("Arduino:  03cba6bc267387d3f534e39936fa3f452dcca9c6036837c43afe7accba6b488");
        Console.WriteLine("Match: " + sig1.Equals("03cba6bc267387d3f534e39936fa3f452dcca9c6036837c43afe7accba6b488"));

        Console.WriteLine("\nTest 2: SITE_001:10:16");
        string msg2 = MACHINE_ID + ":10:16";
        string sig2 = ComputeHmac(msg2, SECRET_KEY);
        Console.WriteLine("Computed: " + sig2);
        Console.WriteLine("Arduino:  e91098144fca17416bc30c9239313668d2cfbafdeae73d7e2c64c163e88561");
        Console.WriteLine("Match: " + sig2.Equals("e91098144fca17416bc30c9239313668d2cfbafdeae73d7e2c64c163e88561"));
    }

    static string ComputeHmac(string message, string secretKey)
    {
        using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secretKey)))
        {
            byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
            StringBuilder sb = new StringBuilder();
            foreach (byte b in hash)
            {
                sb.Append(b.ToString("x2"));
            }
            return sb.ToString();
        }
    }
}
