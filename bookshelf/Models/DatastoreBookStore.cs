﻿// Copyright 2015 Google Inc.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
using Google.Apis.Datastore.v1;
using Google.Apis.Datastore.v1.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace GoogleCloudSamples.Models
{
    public static class DatastoreBookStoreExtensionMethods
    {
        /// <summary>
        /// Make a datastore key given a book's id.
        /// </summary>
        /// <param name="id">A book's id.</param>
        /// <returns>A datastore key.</returns>
        public static Key ToKey(this long id)
        {
            return new Key()
            {
                Path = new PathElement[]
                {
                    new PathElement() { Kind = "Book", Id = (id == 0 ? (long?)null : id) }
                }
            };
        }

        /// <summary>
        /// Make a book id given a datastore key.
        /// </summary>
        /// <param name="key">A datastore key</param>
        /// <returns>A book id.</returns>
        public static long ToId(this Key key)
        {
            return (long)key.Path.First().Id;
        }

        /// <summary>
        /// Get the property from the dict and return null if it isn't there.
        /// </summary>
        /// <param name="properties"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public static Value GetValue(this IDictionary<string, Value> properties, string key)
        {
            Value value;
            bool found = properties.TryGetValue(key, out value);
            return found ? value : null;
        }

        /// <summary>
        /// Creates a new property iff value is not null.
        /// </summary>
        public static Value NewValue(string value)
        {
            return null == value ? null : new Value() { StringValue = value };
        }
        public static Value NewValue(DateTime? value)
        {
            return null == value ? null : new Value() { TimestampValue = value };
        }

        /// <summary>
        /// Create a datastore entity with the same values as book.
        /// </summary>
        /// <param name="book">The book to store in datastore.</param>
        /// <returns>A datastore entity.</returns>
        public static Entity ToEntity(this Book book)
        {
            // TODO: Use reflection so we don't have to modify the code every time we add or drop
            // a property from Book.
            var entity = new Entity();
            entity.Properties =
                new Dictionary<string, Value>();
            entity.Key = book.Id.ToKey();
            entity.Properties["Title"] = NewValue(book.Title);
            entity.Properties["Author"] = NewValue(book.Author);
            entity.Properties["PublishedDate"] = NewValue(book.PublishedDate);
            entity.Properties["ImageUrl"] = NewValue(book.ImageUrl);
            entity.Properties["Description"] = NewValue(book.Description);
            entity.Properties["CreatedBy"] = NewValue(book.CreatedBy);
            entity.Properties["CreateById"] = NewValue(book.CreatedById);
            return entity;
        }

        /// <summary>
        /// Unpack a book from a datastore entity.
        /// </summary>
        /// <param name="entity">An entity retrieved from datastore.</param>
        /// <returns>A book.</returns>
        public static Book ToBook(this Entity entity)
        {
            // TODO: Use reflection so we don't have to modify the code every time we add or drop
            // a property from Book.
            Book book = new Book();
            book.Id = (long)entity.Key.Path.First().Id;
            book.Title = entity.Properties.GetValue("Title")?.StringValue;
            book.Author = entity.Properties.GetValue("Author")?.StringValue;
            book.PublishedDate = (System.DateTime?)entity.Properties.GetValue("PublishedDate")?.TimestampValue;
            book.ImageUrl = entity.Properties.GetValue("ImageUrl")?.StringValue;
            book.Description = entity.Properties.GetValue("Description")?.StringValue;
            book.CreatedBy = entity.Properties.GetValue("CreatedBy")?.StringValue;
            book.CreatedById = entity.Properties.GetValue("CreatedById")?.StringValue;
            return book;
        }
    }

    public class DatastoreBookStore : IBookStore
    {
        private string _projectId;
        private DatastoreService _datastore;

        /// <summary>
        /// Create a new datastore-backed bookstore.
        /// </summary>
        /// <param name="projectId">Your Google Cloud project id</param>
        public DatastoreBookStore(string projectId)
        {
            _projectId = projectId;
            // Use Application Default Credentials.
            var credentials = Google.Apis.Auth.OAuth2.GoogleCredential
                .GetApplicationDefaultAsync().Result;
            credentials = credentials.CreateScoped(new[] {
                DatastoreService.Scope.Datastore
            });
            // Create our connection to datastore.
            _datastore = new DatastoreService(new Google.Apis.Services
                .BaseClientService.Initializer()
            {
                HttpClientInitializer = credentials,
            });
        }

        /// <summary>
        /// A convenience function which commits a mutation to datastore.
        /// Use this function to avoid a lot of boilerplate.
        /// </summary>
        /// <param name="mutation">The change to make to datastore.</param>
        /// <returns>The result of commiting the change.</returns>
        private CommitResponse CommitMutation(Mutation mutation)
        {
            var commitRequest = new CommitRequest()
            {
                Mode = "NON_TRANSACTIONAL",
                Mutations = new[] { mutation },
            };
            return _datastore.Projects.Commit(commitRequest, _projectId)
                .Execute();
        }

        public void Create(Book book)
        {
            var result = CommitMutation(new Mutation()
            {
                Insert = book.ToEntity()
            });
            book.Id = (long)result.MutationResults.First().Key.Path.First().Id;
        }

        public void Delete(long id)
        {
            CommitMutation(new Mutation()
            {
                Delete = id.ToKey()
            });
        }

        public BookList List(int pageSize, string nextPageToken)
        {
            var query = new Query()
            {
                Limit = pageSize,
                Kind = new[] { new KindExpression() { Name = "Book" } },
            };

            if (!string.IsNullOrWhiteSpace(nextPageToken))
                query.StartCursor = nextPageToken;

            var datastoreRequest = _datastore.Projects.RunQuery(
                projectId: _projectId,
                body: new RunQueryRequest() { Query = query }
            );

            var response = datastoreRequest.Execute();
            var results = response.Batch.EntityResults;
            var books = results.Select(result => result.Entity.ToBook());

            return new BookList()
            {
                Books = books,
                NextPageToken = books.Count() == pageSize
                    && response.Batch.MoreResults == "MORE_RESULTS_AFTER_LIMIT"
                    ? response.Batch.EndCursor : null,
            };
        }

        public Book Read(long id)
        {
            var found = _datastore.Projects.Lookup(new LookupRequest()
            {
                Keys = new Key[] { id.ToKey() }
            }, _projectId).Execute().Found;
            if (found == null || found.Count == 0)
            {
                return null;
            }
            return found[0].Entity.ToBook();
        }

        public void Update(Book book)
        {
            CommitMutation(new Mutation()
            {
                Update = book.ToEntity()
            });
        }
    }
}
