using System.Threading;
using System.Threading.Tasks;

namespace Medallion.Threading.Internal.Data
{
    /// <summary>
    /// Implements <see cref="IDbDistributedLock"/> by multiplexing across connections where possible
    /// </summary>
#if DEBUG

    public
#else
    internal
#endif
    sealed class OptimisticConnectionMultiplexingDbDistributedLock : IDbDistributedLock
    {
        private readonly string _name, _connectionString;
        private readonly MultiplexedConnectionLockPool _multiplexedConnectionLockPool;
        private readonly TimeoutValue _keepaliveCadence;
        private readonly IDbDistributedLock _fallbackLock;
        private readonly string? _accessToken;

        public OptimisticConnectionMultiplexingDbDistributedLock(
            string name,
            string connectionString,
            MultiplexedConnectionLockPool multiplexedConnectionLockPool,
            TimeoutValue keepaliveCadence,
            string? accessToken)
        {
            this._name = name;
            this._connectionString = connectionString;
            this._multiplexedConnectionLockPool = multiplexedConnectionLockPool;
            this._keepaliveCadence = keepaliveCadence;
            this._accessToken = accessToken;
            this._fallbackLock = new DedicatedConnectionOrTransactionDbDistributedLock(
                name,
                () => this._multiplexedConnectionLockPool.ConnectionFactory(this._connectionString, accessToken),
                useTransaction: false,
                keepaliveCadence: keepaliveCadence
            );
        }

        public ValueTask<IDistributedSynchronizationHandle?> TryAcquireAsync<TLockCookie>(
            TimeoutValue timeout,
            IDbSynchronizationStrategy<TLockCookie> strategy,
            CancellationToken cancellationToken,
            IDistributedSynchronizationHandle? contextHandle)
            where TLockCookie : class
        {
            // cannot multiplex for updates, since we cannot predict whether or not there will be a request to elevate
            // to an exclusive lock which asks for a long timeout
            if (!strategy.IsUpgradeable && contextHandle == null)
            {
                return this._multiplexedConnectionLockPool.TryAcquireAsync(this._connectionString, this._name, timeout, strategy, keepaliveCadence: this._keepaliveCadence, cancellationToken, this._accessToken);
            }

            // otherwise, fall back to our fallback lock
            return this._fallbackLock.TryAcquireAsync(timeout, strategy, cancellationToken, contextHandle);
        }
    }
}