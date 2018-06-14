using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Configuration;

public static class RedisCacheHelper
{
    //Redis Cache static Property
    private static Lazy<ConnectionMultiplexer> lazyConnection = new Lazy<ConnectionMultiplexer>(() =>
    {
        return ConnectionMultiplexer.Connect(ConfigurationManager.ConnectionStrings["RedisCacheConnection"].ConnectionString);
    });
        
    public static ConnectionMultiplexer Connection
    {
        get
        {
            return lazyConnection.Value;
        }
    }
}