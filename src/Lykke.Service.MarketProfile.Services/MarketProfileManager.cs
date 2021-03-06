﻿using System;
using System.Threading;
using System.Threading.Tasks;
using Common.Log;
using Lykke.Domain.Prices.Contracts;
using Lykke.Domain.Prices.Model;
using Lykke.RabbitMqBroker.Subscriber;
using Lykke.Service.MarketProfile.Core;
using Lykke.Service.MarketProfile.Core.Domain;
using Lykke.Service.MarketProfile.Core.Services;
using Lykke.Service.MarketProfile.Services.RabbitMq;

namespace Lykke.Service.MarketProfile.Services
{
    public class MarketProfileManager :
        IMarketProfileManager,
        IDisposable
    {
        private readonly ILog _log;
        private readonly ApplicationSettings.MarketProfileServiceSettings _settings;
        private readonly IAssetPairsCacheService _cacheService;
        private readonly IAssetPairsRepository _repository;

        private RabbitMqSubscriber<IQuote> _subscriber;
        private Timer _timer;

        public MarketProfileManager(
            ILog log,
            ApplicationSettings.MarketProfileServiceSettings settings,
            IAssetPairsCacheService cacheService,
            IAssetPairsRepository repository)
        {
            _log = log;
            _settings = settings;
            _cacheService = cacheService;
            _repository = repository;
        }

        public void Start()
        {
            try
            {
                UpdateCache().Wait();

                _subscriber = new RabbitMqSubscriber<IQuote>(new RabbitMqSubscriberSettings
                    {
                        ConnectionString = _settings.QuoteFeedRabbitSettings.ConnectionString,
                        QueueName = $"{_settings.QuoteFeedRabbitSettings.ExchangeName}.marketprofileservice",
                        ExchangeName = _settings.QuoteFeedRabbitSettings.ExchangeName
                    })
                    .SetMessageDeserializer(new JsonMessageDeserializer<Quote>())
                    .SetMessageReadStrategy(new MessageReadWithTemporaryQueueStrategy())
                    .Subscribe(ProcessQuote)
                    .SetLogger(_log)
                    .Start();

                _timer = new Timer(PersistCache, null, _settings.CacheSettings.PersistPeriod, Timeout.InfiniteTimeSpan);
            }
            catch (Exception ex)
            {
                _log.WriteErrorAsync(Constants.ComponentName, null, null, ex).Wait();
                throw;
            }
        }

        private async void PersistCache(object state)
        {
            _timer.Change(Timeout.Infinite, Timeout.Infinite);

            try
            {
                var pairs = _cacheService.GetAll();

                await _repository.Write(pairs);
            }
            catch (Exception ex)
            {
                await _log.WriteErrorAsync(Constants.ComponentName, null, null, ex);
            }
            finally
            {
                _timer.Change(_settings.CacheSettings.PersistPeriod, Timeout.InfiniteTimeSpan);
            }
        }

        private async Task UpdateCache()
        {
            var pairs = await _repository.Read();

            _cacheService.InitCache(pairs);
        }

        private async Task ProcessQuote(IQuote entry)
        {
            try
            {
                _cacheService.UpdatePair(entry);
            }
            catch (Exception ex)
            {
                await _log.WriteErrorAsync(Constants.ComponentName, null, null, ex);
            }
        }

        public IAssetPair TryGetPair(string pairCode)
        {
            return _cacheService.TryGetPair(pairCode);
        }

        public IAssetPair[] GetAllPairs()
        {
            return _cacheService.GetAll();
        }

        public void Dispose()
        {
            _timer.Dispose();
            _subscriber.Stop();
        }
    }
}