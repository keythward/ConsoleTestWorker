using System;
using HtmlAgilityPack; // html agility pack
using System.Net;
using System.IO;
using FileHelpers; // filehelpers

namespace ConsoleTestWorker
{
    class Test
    {
        // used to simulate date in DB
        public static DateTime oldDate = new DateTime(2015, 11, 03,14,07,46);
        /*
        public static void Main(string[] args)
        {
            
            
            Console.ReadKey();
        }
        */
        // method to check if an update is needed
        public bool CheckIfNeedUpdate()
        {
            bool well = false;
            string url = "https://www.propertypriceregister.ie/website/npsra/pprweb.nsf/PPRDownloads?OpenForm";
            try
            {
                var Webget = new HtmlWeb();
                var doc = Webget.Load(url);
                var node = doc.DocumentNode.SelectSingleNode("//span[@id='LastUpdated']");
                if (node != null)
                {
                    var innerText = node.InnerText;
                    DateTime lastUpdated = Convert.ToDateTime(innerText);
                    if(!DateTime.Equals(lastUpdated, oldDate)){
                        well = true;
                    }
                }
            }
            catch(System.Net.WebException ex) // exception thrown if url not found
            {
                Console.WriteLine("get html from property price website: "+ex.Message);
            }
            return well;
        }

        // download files
        public void DownloadFiles()
        {
            string tempFilePathWithFileName = Path.GetTempFileName();
            WebClient webClient = new WebClient();
            Console.WriteLine(tempFilePathWithFileName);
            webClient.DownloadFile("https://www.propertypriceregister.ie/website/npsra/ppr/npsra-ppr.nsf/downloads/ppr-2015-10.csv/$file/ppr-2015-10.csv", tempFilePathWithFileName);
            //string readText = File.ReadAllText(tempFilePathWithFileName);
            //Console.WriteLine(readText);
            var engine = new FileHelperEngine<Record>();
            var records = engine.ReadFile(tempFilePathWithFileName);

            foreach (var record in records)
            {
                Console.WriteLine(record.County);
                Console.WriteLine(record.SoldDate.ToString("dd/MM/yyyy"));
                Console.WriteLine(record.Price);
                Console.WriteLine(record.Address);
            }
        }
    }
}