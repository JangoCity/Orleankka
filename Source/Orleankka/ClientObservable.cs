﻿using System;
using System.Linq;
using System.Threading.Tasks;

namespace Orleankka
{
    /// <summary>
    /// Allows clients to receive push-based notifications from actors, ie observing them.
    /// <para>
    /// To teardown created back-channel and delete underlying runtime reference call <see cref="IDisposable.Dispose"/>
    /// </para>
    /// </summary>
    /// <remarks> Instances of this type are not thread safe </remarks>
    public interface IClientObservable : IObservable<Notification>, IDisposable
    {
        /// <summary>
        /// Gets the actual observer proxy that could be passed along with the message.
        /// </summary>
        /// <value>
        /// The observer proxy reference.
        /// </value>
        IActorObserver Proxy
        {
            get;
        }
    }

    /// <summary>
    /// Factory for <see cref="IClientObservable"/>
     /// </summary>
    public sealed class ClientObservable : IClientObservable
    {
        /// <summary>
        /// Creates new <see cref="IClientObservable"/>
        /// </summary>
        /// <returns>New instance of <see cref="IClientObservable"/></returns>
        public static async Task<ClientObservable> Create()
        {
            var observer = new ClientActorObserver();

            var proxy = await ActorObserverFactory.CreateObjectReference(observer);

            return new ClientObservable(observer, proxy);
        }

        readonly ClientActorObserver client;
        readonly IActorObserver proxy;

        ClientObservable(ClientActorObserver client, IActorObserver proxy)
        {
            this.client = client;
            this.proxy = proxy;
        }

        void IDisposable.Dispose()
        {
            ActorObserverFactory.DeleteObjectReference(Proxy);
        }

        public IActorObserver Proxy
        {
            get { return proxy; }
        }

        IDisposable IObservable<Notification>.Subscribe(IObserver<Notification> observer)
        {
            return client.Subscribe(observer);
        }

        class ClientActorObserver : IActorObserver
        {
            IObserver<Notification> observer;

            public IDisposable Subscribe(IObserver<Notification> observer)
            {
                Requires.NotNull(observer, "observer");

                if (this.observer != null)
                    throw new ArgumentException("Susbscription has already been registered", "observer");

                this.observer = observer;

                return new DisposableSubscription(this);
            }

            public void OnNext(Notification notification)
            {
                if (observer != null)
                    observer.OnNext(notification);
            }

            class DisposableSubscription : IDisposable
            {
                readonly ClientActorObserver owner;

                public DisposableSubscription(ClientActorObserver owner)
                {
                    this.owner = owner;
                }

                public void Dispose()
                {
                    owner.observer = null;
                }
            }
        }
    }

    public static class ActorObserverProxyExtensions
    {
        /// <summary>
        /// Notifies the provider that an observer is to receive notifications.
        /// </summary>
        /// <returns>
        /// A reference to an interface that allows observers to stop receiving notifications before the provider has finished sending them.
        /// </returns>
        /// <param name="observer">The instance of observer proxy</param>
        /// <param name="callback">The callback delegate that is to receive notifications</param>
        public static IDisposable Subscribe(this IClientObservable observer, Action<Notification> callback)
        {
            Requires.NotNull(callback, "callback");

            return observer.Subscribe(new DelegateObserver(callback));
        }
       
        class DelegateObserver : IObserver<Notification>
        {
            readonly Action<Notification> callback;

            public DelegateObserver(Action<Notification> callback)
            {
                this.callback = callback;
            }

            public void OnNext(Notification value)
            {
                callback(value);
            }

            public void OnError(Exception error)
            {
                throw new NotImplementedException();
            }

            public void OnCompleted()
            {
                throw new NotImplementedException();
            }
        }
    }
}