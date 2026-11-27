using System.Globalization;
using System.IO.Compression;
using System.Xml;

namespace CarRental.Web.Shared.Data;

public static class SpreadsheetExport
{
    public const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public static byte[] Create(string[] headers, IEnumerable<object?[]> rows)
    {
        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, true))
        {
            Write(zip, "[Content_Types].xml", """
                <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/></Types>
                """);
            Write(zip, "_rels/.rels", """
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>
                """);
            Write(zip, "xl/workbook.xml", """
                <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="Du lieu" sheetId="1" r:id="rId1"/></sheets></workbook>
                """);
            Write(zip, "xl/_rels/workbook.xml.rels", """
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/></Relationships>
                """);
            using var stream = zip.CreateEntry("xl/worksheets/sheet1.xml").Open();
            using var xml = XmlWriter.Create(stream, new XmlWriterSettings { Encoding = new System.Text.UTF8Encoding(false) });
            const string ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            xml.WriteStartElement("worksheet", ns);
            xml.WriteStartElement("sheetViews"); xml.WriteStartElement("sheetView"); xml.WriteAttributeString("workbookViewId", "0");
            xml.WriteStartElement("pane"); xml.WriteAttributeString("ySplit", "1"); xml.WriteAttributeString("topLeftCell", "A2"); xml.WriteAttributeString("state", "frozen"); xml.WriteEndElement(); xml.WriteEndElement(); xml.WriteEndElement();
            xml.WriteStartElement("cols"); xml.WriteStartElement("col"); xml.WriteAttributeString("min", "1"); xml.WriteAttributeString("max", headers.Length.ToString(CultureInfo.InvariantCulture)); xml.WriteAttributeString("width", "25"); xml.WriteAttributeString("customWidth", "1"); xml.WriteEndElement(); xml.WriteEndElement();
            xml.WriteStartElement("sheetData");
            foreach (var row in new[] { headers.Cast<object?>().ToArray() }.Concat(rows))
            {
                xml.WriteStartElement("row");
                foreach (var value in row)
                {
                    xml.WriteStartElement("c");
                    if (value is byte or short or int or long or decimal or float or double)
                        xml.WriteElementString("v", Convert.ToString(value, CultureInfo.InvariantCulture));
                    else
                    {
                        xml.WriteAttributeString("t", "inlineStr"); xml.WriteStartElement("is");
                        xml.WriteElementString("t", value is DateTime date ? date.ToString("yyyy-MM-dd HH:mm") : Convert.ToString(value) ?? ""); xml.WriteEndElement();
                    }
                    xml.WriteEndElement();
                }
                xml.WriteEndElement();
            }
            xml.WriteEndElement(); xml.WriteEndElement();
        }
        return output.ToArray();
    }

    private static void Write(ZipArchive zip, string path, string content)
    {
        using var writer = new StreamWriter(zip.CreateEntry(path).Open());
        writer.Write(content);
    }
}
