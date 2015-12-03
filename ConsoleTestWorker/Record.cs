using FileHelpers;
using System;

namespace ConsoleTestWorker
{
    [DelimitedRecord(",")]
    [IgnoreFirst]
    public class Record
    {
        [FieldTrim(TrimMode.Both)]
        [FieldQuoted('"', QuoteMode.OptionalForRead, MultilineMode.AllowForRead)]
        [FieldConverter(ConverterKind.Date, "dd/MM/yyyy")]
        public DateTime SoldDate;
        [FieldTrim(TrimMode.Both)]
        [FieldQuoted('"', QuoteMode.OptionalForRead, MultilineMode.AllowForRead)]
        public string Address;
        [FieldTrim(TrimMode.Both)]
        [FieldQuoted('"', QuoteMode.OptionalForRead, MultilineMode.AllowForRead)]
        public string PostalCode;
        [FieldTrim(TrimMode.Both)]
        [FieldQuoted('"', QuoteMode.OptionalForRead, MultilineMode.AllowForRead)]
        public string County;
        [FieldTrim(TrimMode.Both)]
        [FieldQuoted('"', QuoteMode.OptionalForRead, MultilineMode.AllowForRead)]
        public string Price;
        [FieldTrim(TrimMode.Both)]
        [FieldQuoted('"', QuoteMode.OptionalForRead, MultilineMode.AllowForRead)]
        public string NMFP;
        [FieldTrim(TrimMode.Both)]
        [FieldQuoted('"', QuoteMode.OptionalForRead, MultilineMode.AllowForRead)]
        public string VAT;
        [FieldTrim(TrimMode.Both)]
        [FieldQuoted('"', QuoteMode.OptionalForRead, MultilineMode.AllowForRead)]
        public string Description;
        [FieldTrim(TrimMode.Both)]
        [FieldQuoted('"', QuoteMode.OptionalForRead, MultilineMode.AllowForRead)]
        [FieldValueDiscarded]
        public string Size;
    }
}