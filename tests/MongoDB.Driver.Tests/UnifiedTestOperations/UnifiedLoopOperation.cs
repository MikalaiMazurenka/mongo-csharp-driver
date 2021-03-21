﻿/* Copyright 2021-present MongoDB Inc.
*
* Licensed under the Apache License, Version 2.0 (the "License");
* you may not use this file except in compliance with the License.
* You may obtain a copy of the License at
*
* http://www.apache.org/licenses/LICENSE-2.0
*
* Unless required by applicable law or agreed to in writing, software
* distributed under the License is distributed on an "AS IS" BASIS,
* WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
* See the License for the specific language governing permissions and
* limitations under the License.
*/

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver.Core.Misc;

namespace MongoDB.Driver.Tests.UnifiedTestOperations
{
    public class UnifiedLoopOperation : IUnifiedOperationWithCreateAndRunOperationCallback
    {
        private readonly UnifiedEntityMap _entityMap;
        private readonly BsonArray _errorDescriptionDocuments;
        private readonly BsonArray _failureDescriptionDocuments;
        private readonly BsonArray _loopOperations;
        private readonly string _storeErrorsAsEntity;
        private readonly string _storeFailuresAsEntity;
        private readonly string _storeIterationsAsEntity;
        private readonly string _storeSuccessesAsEntity;
        private readonly CancellationToken _terminatorCancellationToken;

        public UnifiedLoopOperation(
            UnifiedEntityMap entityMap,
            BsonArray loopOperations,
            string storeErrorsAsEntity,
            string storeFailuresAsEntity,
            string storeIterationsAsEntity,
            string storeSuccessesAsEntity,
            CancellationToken terminatorCancellationToken)
        {
            _entityMap = Ensure.IsNotNull(entityMap, nameof(entityMap));
            _errorDescriptionDocuments = new BsonArray();
            _failureDescriptionDocuments = new BsonArray();
            _loopOperations = Ensure.IsNotNull(loopOperations, nameof(loopOperations));
            _storeErrorsAsEntity = storeErrorsAsEntity;
            _storeFailuresAsEntity = storeFailuresAsEntity;
            _storeIterationsAsEntity = storeIterationsAsEntity;
            _storeSuccessesAsEntity = storeSuccessesAsEntity;
            _terminatorCancellationToken = terminatorCancellationToken;
        }

        public void Execute(Action<BsonDocument, bool, CancellationToken> createAndRunOperationCallback, CancellationToken cancellationToken)
        {
            int iterationsCount = 0;
            int successfulOperationsCount = 0;
            while (!_terminatorCancellationToken.IsCancellationRequested)
            {
                foreach (var operation in _loopOperations.Select(o => o.DeepClone().AsBsonDocument))
                {
                    try
                    {
                        createAndRunOperationCallback(operation, false, cancellationToken);
                        successfulOperationsCount++;
                    }
                    catch (Exception ex)
                    {
                        if (!TryHandleException(ex))
                        {
                            throw;
                        }
                        break;
                    }
                }
                iterationsCount++;
            }

            HandleResults(iterationsCount, successfulOperationsCount);
        }

        public Task ExecuteAsync(Action<BsonDocument, bool, CancellationToken> createAndRunOperationCallback, CancellationToken cancellationToken)
        {
            int iterationsCount = 0;
            int successfulOperationsCount = 0;
            while (!_terminatorCancellationToken.IsCancellationRequested)
            {
                foreach (var operation in _loopOperations.Select(o => o.DeepClone().AsBsonDocument))
                {
                    try
                    {
                        createAndRunOperationCallback(operation, true, cancellationToken);
                        successfulOperationsCount++;
                    }
                    catch (Exception ex)
                    {
                        if (!TryHandleException(ex))
                        {
                            throw;
                        }
                        break;
                    }
                }
                iterationsCount++;
            }

            HandleResults(iterationsCount, successfulOperationsCount);
            return Task.FromResult(true);
        }

        // private methods
        private BsonDocument CreateDocumentFromException(Exception ex)
        {
            return new BsonDocument
            {
                { "error", ex.ToString() },
                { "time", GetCurrentTimeMilliseconds() }
            };

            long GetCurrentTimeMilliseconds()
            {
                return (long)(DateTime.UtcNow - BsonConstants.UnixEpoch).TotalMilliseconds / 1000;
            }
        }

        private void HandleResults(long iterationsCount, long successfulOperationsCount)
        {
            if (_storeSuccessesAsEntity != null)
            {
                _entityMap.SuccessCounts.Add(_storeSuccessesAsEntity, successfulOperationsCount);
            }

            if (_storeIterationsAsEntity != null)
            {
                _entityMap.IterationCounts.Add(_storeIterationsAsEntity, iterationsCount);
            }

            if (_storeFailuresAsEntity != null)
            {
                _entityMap.FailureDocumentsMap.Add(_storeFailuresAsEntity, _failureDescriptionDocuments);
            }

            if (_storeErrorsAsEntity != null)
            {
                _entityMap.ErrorDocumentsMap.Add(_storeErrorsAsEntity, _errorDescriptionDocuments);
            }
        }

        private bool TryHandleException(Exception ex)
        {
            // If the driver's unified test format does not distinguish between errors and failures, and reports one but not the other,
            // the workload executor MUST set the non-reported entry to the empty array.
            if (_storeFailuresAsEntity != null)
            {
                _failureDescriptionDocuments.Add(CreateDocumentFromException(ex));
            }
            else if (_storeErrorsAsEntity != null)
            {
                _errorDescriptionDocuments.Add(CreateDocumentFromException(ex));
            }
            else
            {
                return false;
            }

            return true;
        }
    }

    public class UnifiedLoopOperationBuilder
    {
        private readonly UnifiedEntityMap _entityMap;
        private readonly CancellationToken _terminationCancellationToken;

        public UnifiedLoopOperationBuilder(UnifiedEntityMap entityMap, CancellationToken terminationCancellationToken)
        {
            _entityMap = entityMap;
            _terminationCancellationToken = terminationCancellationToken;
        }

        public UnifiedLoopOperation Build(BsonDocument arguments)
        {
            BsonArray operations = null;
            string storeErrorsAsEntity = null;
            string storeFailuresAsEntity = null;
            string storeIterationsAsEntity = null;
            string storeSuccessesAsEntity = null;

            foreach (var argument in arguments)
            {
                switch (argument.Name)
                {
                    case "operations":
                        operations = argument.Value.AsBsonArray;
                        break;
                    case "storeErrorsAsEntity":
                        storeErrorsAsEntity = argument.Value.AsString;
                        break;
                    case "storeFailuresAsEntity":
                        storeFailuresAsEntity = argument.Value.AsString;
                        break;
                    case "storeIterationsAsEntity":
                        storeIterationsAsEntity = argument.Value.AsString;
                        break;
                    case "storeSuccessesAsEntity":
                        storeSuccessesAsEntity = argument.Value.AsString;
                        break;
                    default:
                        throw new FormatException($"Invalid {nameof(UnifiedLoopOperation)} argument name: '{argument.Name}'.");
                }
            }

            return new UnifiedLoopOperation(
                _entityMap,
                operations,
                storeErrorsAsEntity,
                storeFailuresAsEntity,
                storeIterationsAsEntity,
                storeSuccessesAsEntity,
                _terminationCancellationToken);
        }
    }
}
