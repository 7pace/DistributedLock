using Medallion.Threading.Internal.Data;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace Medallion.Threading.SqlServer
{
    internal static class SqlMultiplexedConnectionLockPool
    {
        public static readonly MultiplexedConnectionLockPool Instance =
            new MultiplexedConnectionLockPool((s, token) => token == null ? new SqlDatabaseConnection(s) : new SqlDatabaseConnection(s, token));
    }
}