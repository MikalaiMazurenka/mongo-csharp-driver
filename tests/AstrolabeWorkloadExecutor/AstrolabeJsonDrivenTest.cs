/* Copyright 2020-present MongoDB Inc.
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
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Bson.TestHelpers;
using MongoDB.Bson.TestHelpers.JsonDrivenTests;

namespace WorkloadExecutor
{
    public class AstrolabeJsonDrivenTest : JsonDrivenTest
    {
        private readonly JsonDrivenTest _wrapped;

        readonly Action _incrementOperationSuccesses;
        readonly Action _incrementOperationErrors;
        readonly Action _incrementOperationFailures;
        // constructors
        public AstrolabeJsonDrivenTest(JsonDrivenTest test, Action incrementOperationSuccesses, Action incrementOperationErrors, Action incrementOperationFailures)
        {
            _wrapped = test;
            _incrementOperationSuccesses = incrementOperationSuccesses;
            _incrementOperationErrors = incrementOperationErrors;
            _incrementOperationFailures = incrementOperationFailures;
        }

        // public methods
        public override void Act(CancellationToken cancellationToken) => _wrapped.Act(cancellationToken);

        public override async Task ActAsync(CancellationToken cancellationToken) => await _wrapped.ActAsync(cancellationToken).ConfigureAwait(false);

        public override void Arrange(BsonDocument document) => _wrapped.Arrange(document);

        public override void Assert()
        {
            var wrappedActualException = _wrapped._actualException();
            if (_wrapped._expectedException() == null)
            {
                if (wrappedActualException != null)
                {
                    if (!(wrappedActualException is OperationCanceledException))
                    {
                        Console.WriteLine($"Operation error (unexpected exception): {wrappedActualException}");
                        _incrementOperationErrors();
                    }
                    return;
                }

                if (_wrapped._expectedResult() == null)
                {
                    _incrementOperationSuccesses();
                }
                else
                {
                    try
                    {
                        AssertResult();
                        _incrementOperationSuccesses();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Operation failure (unexpected result): {ex}");
                        _incrementOperationFailures();
                    }
                }
            }
            else // there currently are no expected exceptions, so this part is slightly speculative
            {
                if (wrappedActualException == null)
                {
                    _incrementOperationErrors();
                    return;
                }

                try
                {
                    AssertException();
                    _incrementOperationSuccesses();
                }
                catch
                {
                    _incrementOperationFailures();
                }
            }
        }


        // protected methods
        protected override void AssertException() => _wrapped.AssertException();

        protected override void AssertResult() => _wrapped.AssertResult();

        protected override void CallMethod(CancellationToken cancellationToken) => _wrapped.CallMethod(cancellationToken);

        protected override Task CallMethodAsync(CancellationToken cancellationToken) =>  _wrapped.CallMethodAsync(cancellationToken);

        protected override void ParseExpectedResult(BsonValue value) => _wrapped.ParseExpectedResult(value);

        protected override void SetArgument(string name, BsonValue value) => _wrapped.SetArgument(name, value);

        protected override void SetArguments(BsonDocument arguments) => _wrapped.SetArguments(arguments);

    }

    internal static class JsonDrivenTestReflector
    {
        public static Exception _actualException(this JsonDrivenTest test)
        {
            return (Exception)Reflector.GetFieldValue(test, nameof(_actualException));
        }

        public static BsonValue _expectedResult(this JsonDrivenTest test)
        {
            return (BsonValue)Reflector.GetFieldValue(test, nameof(_expectedResult));
        }

        public static BsonDocument _expectedException(this JsonDrivenTest test)
        {
            return (BsonDocument)Reflector.GetFieldValue(test, nameof(_expectedException));
        }

        public static void AssertException(this JsonDrivenTest test)
        {
            Reflector.Invoke(test, nameof(AssertException));
        }

        public static void AssertResult(this JsonDrivenTest test)
        {
            Reflector.Invoke(test, nameof(AssertResult));
        }

        public static void CallMethod(this JsonDrivenTest test, CancellationToken cancellationToken)
        {
            Reflector.Invoke(test, nameof(CallMethod), cancellationToken);
        }

        public static Task CallMethodAsync(this JsonDrivenTest test, CancellationToken cancellationToken)
        {
            return (Task)Reflector.Invoke(test, nameof(CallMethodAsync), cancellationToken);
        }

        public static void ParseExpectedResult(this JsonDrivenTest test, BsonValue value)
        {
           Reflector.Invoke(test, nameof(ParseExpectedResult), value);
        }

        public static void SetArgument(this JsonDrivenTest test, string name, BsonValue value)
        {
           Reflector.Invoke(test, nameof(SetArgument), name, value);
        }

        public static void SetArguments(this JsonDrivenTest test, BsonDocument arguments)
        {
           Reflector.Invoke(test, nameof(SetArguments), arguments);
        }

    }
}
