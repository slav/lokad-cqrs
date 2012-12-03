#region (c) 2010-2011 Lokad - CQRS for Windows Azure - New BSD License
// Copyright (c) Lokad 2010-2011, http://www.lokad.com
// This code is released as Open Source under the terms of the New BSD Licence
#endregion

using System;
using Microsoft.WindowsAzure;

namespace Cqrs.Azure.Tests
{
    public static class ConnectionConfig
    {
        public static CloudStorageAccount GetAzureConnnectionString()
        {
            return
                CloudStorageAccount.Parse(
                    "DefaultEndpointsProtocol=https;AccountName=ufabuild;AccountKey=feJnJ6Qf2UDnlQ9swV313V0RMLH9BjSFoWKtCfh8ggxV7l4QOfCG97OavW/K1c/MyUsY2nudUSiE/kskzohy3g==");

            Console.WriteLine("DATA_STORE: " + Environment.GetEnvironmentVariable("Data_Store"));
            if (Environment.GetEnvironmentVariable("Data_Store") != null)
                return CloudStorageAccount.Parse(Environment.GetEnvironmentVariable("Data_Store"));

            return CloudStorageAccount.DevelopmentStorageAccount;
        }

        static readonly Lazy<CloudStorageAccount> Connection = new Lazy<CloudStorageAccount>(GetAzureConnnectionString);


        public static CloudStorageAccount StorageAccount
        {
            get { return Connection.Value; }
        }
    }
}