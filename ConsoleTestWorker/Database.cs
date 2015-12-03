using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.Azure.Documents; // documentdb
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Documents.Linq;
using Newtonsoft.Json; // json
using System.Threading;

namespace ConsoleTestWorker
{
    public class DatabaseConnect
    {
        // db connection strings
        private static string EndpointUri = "https://ppr.documents.azure.com:443/";
        private static string AuthorizationKey = "vsM9HJjmfmWRzyUZ3tIUrtxEk4zM1vqScU09vM47XybdVdSGn4Qll+8jHloQWBREg/cpk+8TruuZZT/aV11cPw==";
        private static string DatabaseId = "ppr_database";
        private static string CollectionId = "ppr_records";
        private static DocumentClient client=new DocumentClient(new Uri(EndpointUri), AuthorizationKey);
        // fields
        public DBRecord document;

        internal DatabaseConnect(string county,List<AlteredRecord> list)
        {
            List<ListObject> List = new List<ListObject>();
            foreach (AlteredRecord ar in list)
            {
                ListObject lo = new ListObject();
                lo.Address = ar.Address;
                lo.Description = ar.Description;
                lo.NotFullMP = ar.NotFullMP;
                lo.PostCode = ar.PostCode;
                lo.Price = ar.Price;
                lo.SoldOn = ar.SoldOn;
                List.Add(lo);
            }
            document = new DBRecord();
            document.id = county;
            document.records = List;
        }
        // create or return a database connection
        public static async Task<Database> GetDatabase(string databaseName)
        {
            if (client.CreateDatabaseQuery().Where(db => db.Id == databaseName).AsEnumerable().Any())
            {
                return client.CreateDatabaseQuery().Where(db => db.Id == databaseName).AsEnumerable().FirstOrDefault();
            }
            return await client.CreateDatabaseAsync(new Database{ Id = databaseName });
        }
        // create or return a collection on a database
        public static async Task<DocumentCollection> GetCollection(Database database, string collName)
         {
            if (client.CreateDocumentCollectionQuery(database.SelfLink).Where(coll => coll.Id == collName).ToArray().Any())
             {
                   return client.CreateDocumentCollectionQuery(database.SelfLink).Where(coll => coll.Id == collName).ToArray().FirstOrDefault();
             }
            return await client.CreateDocumentCollectionAsync(database.SelfLink,new DocumentCollection{ Id = collName });
         }
        // update a modified document
        public async Task<Document> UpdateDocument(DocumentCollection coll, DBRecord record)
        {
            return await client.UpsertDocumentAsync(coll.SelfLink, record);
        }
        // create a document
        public async void CreateDocument(string year)
            {
            var queryDone = false;
            document.id = document.id + year;
            while (!queryDone)
            {
                try
                {
                    Console.WriteLine("creating document for " + document.id);
                    Database database = GetDatabase(DatabaseId).Result;
                    DocumentCollection collection = GetCollection(database, CollectionId).Result;
                    await client.CreateDocumentAsync(collection.SelfLink, document);
                    Console.WriteLine("document done: " + document.id);
                    queryDone = true;
                }
                catch (DocumentClientException documentClientException)
                {
                    var statusCode = (int)documentClientException.StatusCode;
                    if (statusCode == 429 || statusCode == 503)
                        Thread.Sleep(documentClientException.RetryAfter);
                    else
                        throw;
                }
                catch (AggregateException aggregateException)
                {
                    if (aggregateException.InnerException.GetType() == typeof(DocumentClientException))
                    {

                        var docExcep = aggregateException.InnerException as DocumentClientException;
                        var statusCode = (int)docExcep.StatusCode;
                        if (statusCode == 429 || statusCode == 503)
                            Thread.Sleep(docExcep.RetryAfter);
                        else
                            throw;
                    }
                }
            }
            }
        // read a document, modify it, call update method on modified document
        public async void ModifyDocument(string docitem)
        {
            Database database = GetDatabase(DatabaseId).Result;
            DocumentCollection collection = GetCollection(database, CollectionId).Result;
            DBRecord docrecord= (DBRecord) client.CreateDocumentQuery(collection.DocumentsLink).Where(x => x.Id == docitem).AsEnumerable().FirstOrDefault();
            foreach(ListObject lo in document.records)
            {
                docrecord.records.Add(lo);
            }
            Document newdoc = await UpdateDocument(collection, docrecord);
            foreach (ListObject lo in docrecord .records)
            {
                Console.WriteLine(lo.Address);
            }
        }
    }

    // class for record object
    public class DBRecord
    {
        public string id; // county
        public List<ListObject> records; // list of records for county
        // cast document to object
        public static explicit operator DBRecord(Document doc)
        {
            DBRecord rec = new DBRecord();
            rec.id = doc.GetPropertyValue<string>("id");
            rec.records = doc.GetPropertyValue<List<ListObject>>("records");
            return rec;
        }
    }
    // object to populate list
    public class ListObject
    {
        public DateTime SoldOn { get; set; }
        public string Address { get; set; }
        public string PostCode { get; set; }
        public double Price { get; set; }
        public char NotFullMP { get; set; }
        public char Description { get; set; }
    }
}
