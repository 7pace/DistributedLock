﻿using Medallion.Threading.Internal;
using Medallion.Threading.Internal.Data;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Medallion.Threading.SqlServer
{
    internal sealed class SqlDatabaseConnection : DatabaseConnection
    {
        public SqlDatabaseConnection(IDbConnection connection, bool isExternallyOwned = true)
            : base(connection, isExternallyOwned: isExternallyOwned)
        {
        }

        public SqlDatabaseConnection(IDbTransaction transaction)
            : base(transaction, isExternallyOwned: true)
        {
        }

        public SqlDatabaseConnection(string connectionString)
            : this(new SqlConnection(connectionString), isExternallyOwned: false)
        {
        }

        public SqlDatabaseConnection(string connectionString, string accessToken)
            : this(new SqlConnection(connectionString) { AccessToken = accessToken }, isExternallyOwned: false)
        {
        }

        // SQLServer gets no benefit from this
        public override bool ShouldPrepareCommands => false;

        public override bool IsCommandCancellationException(Exception exception)
        {
            const int CanceledNumber = 0;

            // fast path using default SqlClient
            if (exception is SqlException sqlException && sqlException.Number == CanceledNumber)
            {
                return true;
            }

            var exceptionType = exception.GetType();
            // since SqlException is sealed (as of 2020-01-26)
            if (exceptionType.ToString() == "System.Data.SqlClient.SqlException")
            {
                var numberProperty = exceptionType
                    .GetProperty(nameof(SqlException.Number), BindingFlags.Public | BindingFlags.Instance);
                Invariant.Require(numberProperty != null);
                if (numberProperty != null)
                {
                    return Equals(numberProperty.GetValue(exception), CanceledNumber);
                }
            }

            // this shows up when you call DbCommand.Cancel()
            return exception is InvalidOperationException;
        }

        public override Task SleepAsync(TimeSpan sleepTime, CancellationToken cancellationToken, Func<DatabaseCommand, CancellationToken, ValueTask<int>> executor)
        {
            Invariant.Require(sleepTime >= TimeSpan.Zero && sleepTime < TimeSpan.FromDays(1));

            var command = this.CreateCommand();
            command.SetCommandText(@"WAITFOR DELAY @delay");
            command.AddParameter("delay", sleepTime.ToString(@"hh\:mm\:ss\.fff"), DbType.AnsiStringFixedLength);
            command.SetTimeout(sleepTime);

            return executor(command, cancellationToken).AsTask();
        }
    }
}