#load "redisCacheHelper.csx"

using System;
using System.Net;
using Dapper;
using System.Data.SqlClient;
using System.Data;
using System.Configuration;
using System.Threading.Tasks;

public static async Task<HttpResponseMessage> Run(HttpRequestMessage req, TraceWriter log)
{
    log.Info("C# HTTP trigger function processed a request.");

    /*** parse query parameter ***/
    string countrycode = req.GetQueryNameValuePairs()
        .FirstOrDefault(q => string.Compare(q.Key, "countrycode", true) == 0)
        .Value;

    if (countrycode == null)
    {
        /*** Get request body ***/    
        dynamic data = await req.Content.ReadAsAsync<object>();    
        countrycode = data?.countrycode;
    }

    string jsonResult = string.Empty;
    try
    {
        /*** Connecting to Redis Cache. ***/
        var cache = RedisCacheHelper.Connection.GetDatabase();

        /*** Retrieve all country information ***/
        if(string.IsNullOrEmpty(countrycode))
        {
            /*** Get information from Redis cache. ***/
            jsonResult = cache.StringGet("CountryInfo");
            //log.Info($"Redis Cache - Stored Info:");

            if (string.IsNullOrEmpty(jsonResult))
            {
                /*** Connecting to Azure SQL Database. ***/
                var connectionString = ConfigurationManager.ConnectionStrings["AzureSqlConnection-Dev"].ConnectionString;
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    using (SqlCommand cmd = new SqlCommand())
                    {                    
                        cmd.CommandText = "twoconnect.CountryInformationAll_lst";//Stored Procedure name.
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Connection = connection;

                        connection.Open();
                        SqlDataReader reader = cmd.ExecuteReader();
                        while (reader.Read())
                        {
                            jsonResult = reader[0].ToString();                                                

                            /*** store information into redis cache. ***/
                            cache.StringSet("CountryInfo", jsonResult, TimeSpan.FromMinutes(20));                            
                        }
                    }
                }
            }
        }
        else //Retrieve country information by countryCode (ISO3)
        {
            /*** Get information from Redis cache based on the key sent in the parameter countryCode. ***/
            string redisKey = "CI_" + countrycode;
            jsonResult = cache.StringGet(redisKey);

            if (string.IsNullOrEmpty(jsonResult))
            {
                /*** Connecting to Azure SQL Database. ***/
                var connectionString = ConfigurationManager.ConnectionStrings["AzureSqlConnection-Dev"].ConnectionString;
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    using (SqlCommand cmd = new SqlCommand())
                    {                    
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.CommandText = "[twoconnect].[CountryInformationByCountryCode_lst]";//Stored Procedure name.
                        cmd.Parameters.Add("@countryCode", SqlDbType.NVarChar).Value = countrycode;//Parameters.                        
                        cmd.Connection = connection;

                        connection.Open();
                        SqlDataReader reader = cmd.ExecuteReader();
                        while (reader.Read())
                        {
                            jsonResult = reader[0].ToString();                                                

                            /*** store information into redis cache. ***/
                            cache.StringSet(redisKey, jsonResult, TimeSpan.FromMinutes(20));                            
                        }
                    }
                }
            }
        }
    }
    catch(Exception ex)
    {
        return req.CreateResponse(HttpStatusCode.InternalServerError, ex.Message);
    }

    /*** Create response. ***/
    HttpResponseMessage result = new HttpResponseMessage(HttpStatusCode.OK);
    result.Content = new StringContent(jsonResult);
    result.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

    return result;
}
