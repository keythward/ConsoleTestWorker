using System;
using FileHelpers; // filehelpers
using HtmlAgilityPack; // html agility pack
using System.Net;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ConsoleTestWorker
{
    class Test2
    {
        // list of counties
        public enum County { Kerry,Cork,Limerick,Tipperary,Waterford,Kilkenny,Wexford,Laois,Carlow,Kildare,Wicklow,
        Offaly,Dublin,Meath,Westmeath,Louth,Monaghan,Cavan,Longford,Donegal,Leitrim,Sligo,Roscommon,Mayo,Galway,Clare};
        public static void Main()
        {
            string url = "https://www.propertypriceregister.ie/website/npsra/ppr/npsra-ppr.nsf/downloads/ppr-2015.csv/$file/ppr-2015.csv";

            string proceed = DownloadFile(url);
            if (proceed != null)
            {
                List<Record> returnedList = ConvertCSV(proceed);
                if (returnedList == null) // null returned so exception thrown
                {
                    // reschedule job for tommorow
                }
                else if (returnedList.Count == 0) // if list is empty
                {
                    // stop program
                }
                else
                {
                    // sort list
                    SortRecords(returnedList);
                }
            }
            else
            {
                // reschedule job for tommorow
            }
            Console.ReadKey();
        }
        // download file from ppr website
        public static string DownloadFile(string url)
        {
            string bad = null;
            try
            {
                string tempFilePathWithFileName = Path.GetTempFileName();
                using (WebClient webClient = new WebClient())
                {
                    webClient.DownloadFile(url, tempFilePathWithFileName);
                }
                return tempFilePathWithFileName;
            }
            catch (IOException e)
            {
                Console.WriteLine(e.Message);
                return bad;
            }
            catch (WebException we)
            {
                Console.WriteLine(we.Message);
                return bad;
            }
        }
        // covert the csv file to record objects
        public static List<Record> ConvertCSV(string filename)
        {
            List<Record> recordList = new List<Record>();
            try
            {
                var engine = new FileHelperEngine<Record>();
                // switch error mode on - skip any records that cause error and save them
                engine.ErrorManager.ErrorMode = ErrorMode.SaveAndContinue;
                var records = engine.ReadFile(filename);
                // if any errors send them to console/log (temp file then delete)
                string tempFile = Path.GetTempFileName();
                if (engine.ErrorManager.HasErrors)
                    engine.ErrorManager.SaveErrors(tempFile);
                ErrorInfo[] errors = ErrorManager.LoadErrors(tempFile);
                foreach (var err in errors)
                {
                    Console.WriteLine();
                    Console.WriteLine("Error on Line number: {0}", err.LineNumber);
                    Console.WriteLine("Record causing the problem: {0}", err.RecordString);
                    Console.WriteLine("Complete exception information: {0}", err.ExceptionInfo.ToString());
                }
                File.Delete(tempFile);
                // sort records
                foreach (var record in records)
                {
                    recordList.Add(record);
                }
                return recordList;
            }
            catch (IOException e)
            {
                Console.WriteLine(e.Message);
                return null;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return null;
            }

        }
        // filter the records into new altered record objects and sort by county
        public static void SortRecords(List<Record> list)
        {
            List<AlteredRecord> altered = new List<AlteredRecord>();
            foreach (Record r in list)
            {
                AlteredRecord ar = new AlteredRecord();
                ar.SoldOn = r.SoldDate;
                ar.County = r.County;
                // take ? out of price and change to double
                // no need to catch format exception
                string sub = r.Price.Substring(1);
                double price = Convert.ToDouble(sub);
                ar.Price = price;
                // take first char from string for NFMP
                ar.NotFullMP = r.NMFP[0];
                // shorten description to char of N/S (new/second hand)
                if (r.Description.StartsWith("N"))
                {
                    ar.Description = 'N';
                }
                if (r.Description.StartsWith("S"))
                {
                    ar.Description = 'S';
                }
                // take vat column out and phase it into the price (if No then take 13.5% off price)
                if (r.VAT.StartsWith("N"))
                {
                    price = price * 0.865;
                    ar.Price = price;
                }
                // sort address and postal code if dublin
                AddressHolder addresssorted = SortAddress(r.Address, r.County, r.PostalCode);
                ar.Address = addresssorted.Address;
                ar.PostCode = addresssorted.PostalCode;
                // add altered record to altered record list
                altered.Add(ar);
            }
            // send to method to divide up list and send to database
            //DivideAndSave(altered);                                         // CREATE DOCUMENTS
            DivideAndSave2(altered);                                        // UPDATE DOCUMENTS
        }
        // sort address and postal code
        public static AddressHolder SortAddress(string address, string county, string pc)
        {
            AddressHolder add = new AddressHolder();
            // break the address up into its parts separated by a comma
            string[] parts = address.Split(',').Select(sValue => sValue.Trim().ToLower()).ToArray();
            // county to lowercase
            string countyLower = county.ToLower();
            string pcLower = pc.ToLower();
            add.PostalCode = pcLower;
            // number elements in array
            int num = parts.Length;
            // not for dublin check if last element of address matches the county, if so get rid of
            if (!countyLower.Equals("dublin")) {
                if (parts[num - 1].Equals(countyLower) || parts[num - 1].Equals("co " + countyLower) ||
                    parts[num - 1].Equals("co. " + countyLower) || parts[num - 1].Equals("county " + countyLower)
                    || parts[num - 1].Equals("co." + countyLower))
                {
                    num--; // take last element away
                }
            }
            else // Sort out dublin
            {
                if (parts[num - 1].Equals(countyLower) || parts[num - 1].Equals("co " + countyLower) ||
                    parts[num - 1].Equals("co. " + countyLower) || parts[num - 1].Equals("county " + countyLower)
                    || parts[num - 1].Equals(pcLower) || parts[num - 1].Equals("co." + countyLower))
                {
                    if (parts[num - 1].Any(char.IsDigit)) // if it contains numbers its the postal code
                    {
                        add.PostalCode = parts[num - 1];
                    }
                    num--;
                }
                // try find postal code from address if does not already have a value for it
                if (add.PostalCode.Equals(""))
                {
                    if (FindPostalCode(parts[num - 1]) != null)
                    {
                        add.PostalCode = FindPostalCode(parts[num - 1]);
                    }
                    else // check if post code is in address at end of address line
                    {
                        string last = parts[num - 1];
                        int lastLenght = last.Length;
                        if (lastLenght >= 6)
                        {
                            last = parts[num - 1].Substring(0, 6);
                        }
                        if (last.Equals("dublin"))
                        {
                            add.PostalCode = parts[num - 1];
                            num--;
                        }
                    }
                }
            }
            // put address back together
            string complete = "";
            for (int i = 0; i < num; i++)
            {
                complete += parts[i] + ",";
            }
            add.Address = complete.Remove(complete.Length - 1);
            // return address
            return add;
        }
        // container to hold address and postal code
        public struct AddressHolder
        {
            public string Address { get; set; }
            public string PostalCode { get; set; }
        }
        // find postal code or return null
        public static string FindPostalCode(string line)
        {
            string found = null;
            if (postalCodes.ContainsKey(line))
            {
                found = postalCodes[line];
            }
            return found;
        }
        // postal codes matching areas
        public static Dictionary<string, string> postalCodes = new Dictionary<string, string>()
            { {"north wall", "dublin 1"},
                { "summerhill","dublin 1"},
                { "parnell street", "dublin 1"},
                { "templebar", "dublin 1"},
                { "ballybough","dublin 3"},
                { "cloniffe","dublin 3"},
                { "clontarf","dublin 3"},
                { "dollymount", "dublin 3"},
                { "east wall", "dublin 3"},
                { "fairview", "dublin 3"},
                { "marino","dublin 3"},
                { "killester", "dublin 3"},
                { "college green", "dublin 2"},
                { "merrion square", "dublin 2"},
                { "st. stephens green", "dublin 2"},
                { "ballsbridge", "dublin 4"},
                { "donnybrook", "dublin 4"},
                { "irishtown", "dublin 4"},
                { "merrion", "dublin 4"},
                { "pembroke", "dublin 4"},
                { "ringsend", "dublin 4"},
                { "sandymount", "dublin 4"},
                { "north strand", "dublin 3"},
                { "artane", "dublin 5"},
                { "harmonstown", "dublin 5"},
                { "donnycarney", "dublin 5"},
                { "raheny", "dublin 5"},
                { "dartry", "dublin 6"},
                { "ranelagh", "dublin 6"},
                { "rathmines", "dublin 6"},
                { "rathgar", "dublin 6"},
                { "harolds cross", "dublin 6w"},
                { "templeogue", "dublin 6w"},
                { "terenure", "dublin 6w"},
                { "arbour hill", "dublin 7"},
                { "cabra", "dublin 7"},
                { "phibsboro", "dublin 7"},
                { "four courts", "dublin 7"},
                { "navan road", "dublin 7"},
                { "dolphins barn", "dublin 8"},
                { "rialto", "dublin 8"},
                { "inchicore", "dublin 8"},
                { "island bridge", "dublin 8"},
                { "kilmainham", "dublin 8"},
                { "portobello", "dublin 8"},
                { "the coombe", "dublin 8"},
                { "beaumont", "dublin 9"},
                { "drumcondra", "dublin 9"},
                { "santry", "dublin 9"},
                { "whitehall", "dublin 9"},
                { "glasnevin", "dublin 9"},
                { "ballyfermot", "dublin 10"},
                { "ballygall", "dublin 11"},
                { "cappagh", "dublin 11"},
                { "cremore", "dublin 11"},
                { "dubber", "dublin 11"},
                { "finglas", "dublin 11"},
                { "jamestown", "dublin 11"},
                { "kilshane", "dublin 11"},
                { "wadelai", "dublin 11"},
                { "bluebell", "dublin 12"},
                { "crumlin", "dublin 12"},
                { "drimnagh", "dublin 12"},
                { "walkinstown", "dublin 12"},
                { "baldoyle","dublin 13"},
                { "donaghmede", "dublin 13"},
                { "sutton", "dublin 13"},
                { "howth", "dublin 13"},
                { "churchtown", "dublin 14"},
                { "dundrum", "dublin 14"},
                { "goatstown", "dublin 14"},
                { "roebuck", "dublin 14"},
                { "windy arbour", "dublin 14"},
                { "clonskeagh", "dublin 14"},
                { "rathfarnham", "dublin 14"},
                { "blanchardstown", "dublin 15"},
                { "castleknock", "dublin 15"},
                { "clonee", "dublin 15"},
                { "clonsilla", "dublin 15"},
                { "corduff", "dublin 15"},
                { "mulhuddart", "dublin 15"},
                { "tyrrelstown", "dublin 15"},
                { "ballinteer", "dublin 16"},
                { "kilmashogue", "dublin 16"},
                { "knocklyon", "dublin 16"},
                { "rockbrook", "dublin 16"},
                { "whitechurch", "dublin 16"},
                { "belcamp", "dublin 17"},
                { "balgriffin", "dublin 17"},
                { "clonshaugh", "dublin 17"},
                { "darndale", "dublin 17"},
                { "riverside", "dublin 17"},
                { "clare hall", "dublin 17"},
                { "cabinteely", "dublin 18"},
                { "carrickmines", "dublin 18"},
                { "foxrock","dublin 18"},
                { "kilternan", "dublin 18"},
                { "sandyford", "dublin 18"},
                { "ticknock", "dublin 18"},
                { "ballyedmonduff", "dublin 18"},
                { "stepaside", "dublin 18"},
                { "leopardstown", "dublin 18"},
                { "loughlinstown", "dublin 18"},
                { "lucan", "dublin 20"},
                { "chapelizod", "dublin 20"},
                { "palmerstown", "dublin 20"},
                { "adamstown", "dublin 20"},
                { "neilstown", "dublin 22"},
                { "clondalkin", "dublin 22"},
                { "bawnogue", "dublin 22"},
                { "firhouse", "dublin 24"},
                { "jobstown", "dublin 24"},
                { "kilnamanagh", "dublin 24"},
                { "oldbawn", "dublin 24"},
                { "tallaght", "dublin 24"},
                { "springfield", "dublin 24"},
                { "booterstown", "county dublin"},
                { "williamstown", "county dublin"},
                { "salthill", "county dublin"},
                { "monkstown", "county dublin"},
                { "mt. merrion", "county dublin"},
                { "blackrock", "county dublin"},
                { "stillorgan", "county dublin"},
                { "kilmacud", "county dublin"},
                { "deans grange", "county dublin"},
                { "newtown park", "county dublin"},
                { "sandycove","county dublin"},
                { "mounttown", "county dublin"},
                { "sallynoggin", "county dublin"},
                { "glasthule", "county dublin"},
                { "dun laoghaire", "county dublin"},
                { "dunlaoghaire", "county dublin"},
                { "glenageary","county dublin"},
                { "kill-o-the-grange", "county dublin"},
                { "dalkey", "county dublin"},
                { "killiney", "county dublin"},
                { "ballybrack", "county dublin"},
                { "dunlaoire", "county dublin"},
                { "malahide", "county dublin"},
                { "swords", "county dublin"},
                { "newcastle", "county dublin"},
                { "balbriggan", "county dublin"},
                { "donabate", "county dublin"},
                { "skerries", "county dublin"},
                { "portmarnock", "county dublin"},
                { "rathcoole", "county dublin"},
                { "saggart", "county dublin"},
                { "shankill", "county dublin"}};
        // divide list up into counties and add to database                 //CREATE
        public static void DivideAndSave(List<AlteredRecord> list)
        {
            List<AlteredRecord> templist = new List<AlteredRecord>();
            List<AlteredRecord> templist1 = new List<AlteredRecord>();
            List<AlteredRecord> templist2 = new List<AlteredRecord>();
            for (County co = County.Kerry; co <= County.Clare; co++)
            {
                if (co == County.Dublin) // Dublin is too large and needs to be uploaded to database in months
                {
                    foreach (AlteredRecord ar in list)
                    {
                        if (ar.County.Equals(co.ToString()))
                        {
                            templist.Add(ar);
                        }
                    }
                    List<AlteredRecord> templist_a = new List<AlteredRecord>();
                    List<AlteredRecord> templist_b = new List<AlteredRecord>();
                    List<AlteredRecord> templist_c = new List<AlteredRecord>();
                    List<AlteredRecord> templist_d = new List<AlteredRecord>();
                    List<AlteredRecord> templist_e = new List<AlteredRecord>();
                    List<AlteredRecord> templist_f = new List<AlteredRecord>();
                    List<AlteredRecord> templist_g = new List<AlteredRecord>();
                    List<AlteredRecord> templist_h = new List<AlteredRecord>();
                    List<AlteredRecord> templist_i = new List<AlteredRecord>();
                    List<AlteredRecord> templist_j = new List<AlteredRecord>();
                    List<AlteredRecord> templist_k = new List<AlteredRecord>();
                    List<AlteredRecord> templist_l = new List<AlteredRecord>();
                    foreach (AlteredRecord ar in templist) // divide templist up into months
                    {
                        if (ar.SoldOn.Month == 1)
                        {
                            templist_a.Add(ar);
                        }
                        else if (ar.SoldOn.Month == 2)
                        {
                            templist_b.Add(ar);
                        }
                        else if (ar.SoldOn.Month == 3)
                        {
                            templist_c.Add(ar);
                        }
                        else if (ar.SoldOn.Month == 4)
                        {
                            templist_d.Add(ar);
                        }
                        else if (ar.SoldOn.Month == 5)
                        {
                            templist_e.Add(ar);
                        }
                        else if (ar.SoldOn.Month == 6)
                        {
                            templist_f.Add(ar);
                        }
                        else if (ar.SoldOn.Month == 7)
                        {
                            templist_g.Add(ar);
                        }
                        else if (ar.SoldOn.Month == 8)
                        {
                            templist_h.Add(ar);
                        }
                        else if (ar.SoldOn.Month == 9)
                        {
                            templist_i.Add(ar);
                        }
                        else if (ar.SoldOn.Month == 10)
                        {
                            templist_j.Add(ar);
                        }
                        else if (ar.SoldOn.Month == 11)
                        {
                            templist_k.Add(ar);
                        }
                        else
                        {
                            templist_l.Add(ar);
                        }
                    }
                    // save templist in proper document
                    DatabaseConnect dba = new DatabaseConnect(co.ToString(), templist_a);
                    DatabaseConnect dbb = new DatabaseConnect(co.ToString(), templist_b);
                    DatabaseConnect dbc = new DatabaseConnect(co.ToString(), templist_c);
                    DatabaseConnect dbd = new DatabaseConnect(co.ToString(), templist_d);
                    DatabaseConnect dbe = new DatabaseConnect(co.ToString(), templist_e);
                    DatabaseConnect dbf = new DatabaseConnect(co.ToString(), templist_f);
                    DatabaseConnect dbg = new DatabaseConnect(co.ToString(), templist_g);
                    DatabaseConnect dbh = new DatabaseConnect(co.ToString(), templist_h);
                    DatabaseConnect dbi = new DatabaseConnect(co.ToString(), templist_i);
                    DatabaseConnect dbj = new DatabaseConnect(co.ToString(), templist_j);
                    DatabaseConnect dbk = new DatabaseConnect(co.ToString(), templist_k);
                    DatabaseConnect dbl = new DatabaseConnect(co.ToString(), templist_l);
                    string year = "2015_1";
                    dba.CreateDocument(year);
                    year = "2015_2";
                    dbb.CreateDocument(year);
                    year = "2015_3";
                    dbc.CreateDocument(year);
                    year = "2015_4";
                    dbd.CreateDocument(year);
                    year = "2015_5";
                    dbe.CreateDocument(year);
                    year = "2015_6";
                    dbf.CreateDocument(year);
                    year = "2015_7";
                    dbg.CreateDocument(year);
                    year = "2015_8";
                    dbh.CreateDocument(year);
                    year = "2015_9";
                    dbi.CreateDocument(year);
                    year = "2015_10";
                    dbj.CreateDocument(year);
                    year = "2015_11";
                    dbk.CreateDocument(year);
                    year = "2015_12";
                    dbl.CreateDocument(year);
                    templist.Clear();
                    
                }
                else // rest of ireland
                {
                    foreach (AlteredRecord ar in list)
                    {
                        if (ar.County.Equals(co.ToString()))
                        {
                            templist.Add(ar);
                        }
                    }
                    foreach (AlteredRecord ar in templist) // divide in 2
                    {
                        if (ar.SoldOn.Month == 1 || ar.SoldOn.Month == 2 || ar.SoldOn.Month == 3 || ar.SoldOn.Month == 4 ||
                            ar.SoldOn.Month == 5 || ar.SoldOn.Month == 6)
                        {
                            templist1.Add(ar);
                        }
                        else
                        {
                            templist2.Add(ar);
                        }
                    }
                    // save templist in proper document
                    DatabaseConnect db = new DatabaseConnect(co.ToString(), templist1);
                    DatabaseConnect db2 = new DatabaseConnect(co.ToString(), templist2);
                    string year = "2015_A";
                    db.CreateDocument(year);
                    year = "2015_B";
                    db2.CreateDocument(year);
                    // empty templists
                    templist.Clear();
                    templist1.Clear();
                    templist2.Clear();
                }
            }

        }

        // divide list up into counties and add to database                 //UPDATE
        public static void DivideAndSave2(List<AlteredRecord> list)
        {
            List<AlteredRecord> templist = new List<AlteredRecord>();
            List<AlteredRecord> templist1 = new List<AlteredRecord>();
            List<AlteredRecord> templist2 = new List<AlteredRecord>();
            for (County co = County.Kerry; co <= County.Clare; co++)
            {
                if (co == County.Dublin) // Dublin is too large and needs to be uploaded to database in months
                {
                    foreach (AlteredRecord ar in list)
                    {
                        if (ar.County.Equals(co.ToString()))
                        {
                            templist.Add(ar);
                        }
                    }
                    List<AlteredRecord> templist_a = new List<AlteredRecord>();
                    List<AlteredRecord> templist_b = new List<AlteredRecord>();
                    List<AlteredRecord> templist_c = new List<AlteredRecord>();
                    List<AlteredRecord> templist_d = new List<AlteredRecord>();
                    List<AlteredRecord> templist_e = new List<AlteredRecord>();
                    List<AlteredRecord> templist_f = new List<AlteredRecord>();
                    List<AlteredRecord> templist_g = new List<AlteredRecord>();
                    List<AlteredRecord> templist_h = new List<AlteredRecord>();
                    List<AlteredRecord> templist_i = new List<AlteredRecord>();
                    List<AlteredRecord> templist_j = new List<AlteredRecord>();
                    List<AlteredRecord> templist_k = new List<AlteredRecord>();
                    List<AlteredRecord> templist_l = new List<AlteredRecord>();
                    foreach (AlteredRecord ar in templist) // divide templist up into months
                    {
                        if (ar.SoldOn.Month == 1)
                        {
                            templist_a.Add(ar);
                        }
                        else if (ar.SoldOn.Month == 2)
                        {
                            templist_b.Add(ar);
                        }
                        else if (ar.SoldOn.Month == 3)
                        {
                            templist_c.Add(ar);
                        }
                        else if (ar.SoldOn.Month == 4)
                        {
                            templist_d.Add(ar);
                        }
                        else if (ar.SoldOn.Month == 5)
                        {
                            templist_e.Add(ar);
                        }
                        else if (ar.SoldOn.Month == 6)
                        {
                            templist_f.Add(ar);
                        }
                        else if (ar.SoldOn.Month == 7)
                        {
                            templist_g.Add(ar);
                        }
                        else if (ar.SoldOn.Month == 8)
                        {
                            templist_h.Add(ar);
                        }
                        else if (ar.SoldOn.Month == 9)
                        {
                            templist_i.Add(ar);
                        }
                        else if (ar.SoldOn.Month == 10)
                        {
                            templist_j.Add(ar);
                        }
                        else if (ar.SoldOn.Month == 11)
                        {
                            templist_k.Add(ar);
                        }
                        else
                        {
                            templist_l.Add(ar);
                        }
                    }
                    // save templist in proper document
                    DatabaseConnect dba = new DatabaseConnect(co.ToString(), templist_a);
                    DatabaseConnect dbb = new DatabaseConnect(co.ToString(), templist_b);
                    DatabaseConnect dbc = new DatabaseConnect(co.ToString(), templist_c);
                    DatabaseConnect dbd = new DatabaseConnect(co.ToString(), templist_d);
                    DatabaseConnect dbe = new DatabaseConnect(co.ToString(), templist_e);
                    DatabaseConnect dbf = new DatabaseConnect(co.ToString(), templist_f);
                    DatabaseConnect dbg = new DatabaseConnect(co.ToString(), templist_g);
                    DatabaseConnect dbh = new DatabaseConnect(co.ToString(), templist_h);
                    DatabaseConnect dbi = new DatabaseConnect(co.ToString(), templist_i);
                    DatabaseConnect dbj = new DatabaseConnect(co.ToString(), templist_j);
                    DatabaseConnect dbk = new DatabaseConnect(co.ToString(), templist_k);
                    DatabaseConnect dbl = new DatabaseConnect(co.ToString(), templist_l);
                    string year = "2014_1";
                    //dba.CreateDocument(year);
                    year = "2014_2";
                    //dbb.CreateDocument(year);
                    year = "2014_3";
                    //dbc.CreateDocument(year);
                    year = "2014_4";
                    //dbd.CreateDocument(year);
                    year = "2014_5";
                    //dbe.CreateDocument(year);
                    year = "2014_6";
                    //dbf.CreateDocument(year);
                    year = "2014_7";
                    //dbg.CreateDocument(year);
                    year = "2014_8";
                    //dbh.CreateDocument(year);
                    year = "2014_9";
                    //dbi.CreateDocument(year);
                    year = "2014_10";
                    //dbj.CreateDocument(year);
                    year = "2015_11";
                    dbk.ModifyDocument(year);
                    year = "2014_12";
                    //dbl.CreateDocument(year);
                    templist.Clear();

                }
                else // rest of ireland
                {
                    foreach (AlteredRecord ar in list)
                    {
                        if (ar.County.Equals(co.ToString()))
                        {
                            templist.Add(ar);
                        }
                    }
                    foreach (AlteredRecord ar in templist) // divide in 2
                    {
                        if (ar.SoldOn.Month == 1 || ar.SoldOn.Month == 2 || ar.SoldOn.Month == 3 || ar.SoldOn.Month == 4 ||
                            ar.SoldOn.Month == 5 || ar.SoldOn.Month == 6)
                        {
                            templist1.Add(ar);
                        }
                        else
                        {
                            templist2.Add(ar);
                        }
                    }
                    // save templist in proper document
                    DatabaseConnect db = new DatabaseConnect(co.ToString(), templist1);
                    DatabaseConnect db2 = new DatabaseConnect(co.ToString(), templist2);
                    string year = "2014_A";
                    //db.CreateDocument(year);
                    year = "2014_B";
                    //db2.CreateDocument(year);
                    //db.ModifyDocument(co.ToString());
                    // empty templists
                    templist.Clear();
                    templist1.Clear();
                    templist2.Clear();
                }
            }

        }
    }
}
// https://www.propertypriceregister.ie/website/npsra/ppr/npsra-ppr.nsf/downloads/ppr-2015-10.csv/$file/ppr-2015-10.csv
