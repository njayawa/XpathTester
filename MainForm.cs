using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Xml.XPath;
using System.IO;
using System.Xml;
using System.Globalization;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace XPathTester
{
    public partial class MainForm : Form
    {
        public MainForm()
        {
            InitializeComponent();

            // This regular expression is derived from the XML definition at:
            //  http://www.w3.org/TR/REC-xml/
            // It is used to strip out an explicit "encoding" declaration, which is a problem
            //  when processing XML copied from the clipboard.
            XmlEncodingRegex = new Regex(@"<\?xml[ \r\n\t]+version[ \r\n\t]*=[ \r\n\t]*['""]([a-zA-Z0-9_.:-]+)['""][ \r\n\t]+encoding[ \r\n\t]*=[ \r\n\t]*['""][a-zA-Z0-9._-]+['""]");
        }

        /// <summary>
        /// The regular expression used to strip out explicit "encoding" declarations.
        /// </summary>
        private Regex XmlEncodingRegex;

        /// <summary>
        /// Adds an exception to a string error.
        /// </summary>
        /// <param name="sb">The error buffer to which to add.</param>
        /// <param name="err">The exception to add to the error description.</param>
        private void FormatException(StringBuilder sb, XmlException err)
        {
            sb.AppendLine("$exception [" + err.GetType().Name + "]: " + err.Message);
            if (err.LineNumber > 0 && err.LineNumber <= textBoxXML.Lines.Length)
            {
                // If we have a valid error position, then display the problematic
                //  XML line with a pointer to where the error was detected.
                // Note: This code assumes a monospaced font is used to display the error.
                sb.AppendLine(textBoxXML.Lines[err.LineNumber - 1]);
                sb.AppendLine("^".PadLeft(err.LinePosition, ' '));
            }
            sb.AppendLine(err.StackTrace);
        }

        /// <summary>
        /// Adds an exception to a string error.
        /// </summary>
        /// <param name="sb">The error buffer to which to add.</param>
        /// <param name="err">The exception to add to the error description.</param>
        private void FormatException(StringBuilder sb, Exception err)
        {
            sb.AppendLine("$exception [" + err.GetType().Name + "]: " + err.Message);
            sb.AppendLine(err.StackTrace);
        }

        /// <summary>
        /// Sets the results to display an error message.
        /// </summary>
        /// <param name="err">The exception to display.</param>
        /// <param name="context">What we were doing when the exception was thrown.</param>
        private void SetResultText(Exception err, string context)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("Error " + context + ": ");
            XmlException xmlException = err as XmlException;
            if (xmlException != null)
                FormatException(sb, xmlException);
            else
                FormatException(sb, err);
            sb.Append(Environment.NewLine + "End of line.");
            textBoxResults.Text = sb.ToString();
        }

        /// <summary>
        /// Pretty-prints a string value, using C-style escape sequences.
        /// Note: this function is not Unicode-friendly and should not be used in production code.
        /// </summary>
        /// <param name="input">The value to dump.</param>
        /// <returns>The pretty-printed value.</returns>
        private static string DumpString(string input)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("\"");
            foreach (char ch in input)
            {
                if (ch == '\\')
                    sb.Append("\\\\");
                else if (ch == '\r')
                    sb.Append("\\r");
                else if (ch == '\n')
                    sb.Append("\\n");
                else if (ch == '\"')
                    sb.Append("\\\"");
                else
                    sb.Append(ch);
            }
            sb.Append("\"");
            return sb.ToString();
        }

        /// <summary>
        /// Adds a value to the result string.
        /// </summary>
        /// <param name="sb">The buffer to which to add.</param>
        /// <param name="result">The value to add to the result string.</param>
        private void FormatResult(StringBuilder sb, string result)
        {
            // Output the length of the string explicitly; this helps confirm empty string results
            sb.AppendLine("{Length=" + result.Length.ToString() + "}");
            sb.AppendLine();
            // Pretty-print the string value
            sb.AppendLine(DumpString(result));
        }

        /// <summary>
        /// Adds a value to the result string.
        /// </summary>
        /// <param name="sb">The buffer to which to add.</param>
        /// <param name="result">The value to add to the result string.</param>
        private void FormatResult(StringBuilder sb, bool result)
        {
            sb.AppendLine(result.ToString());
        }

        /// <summary>
        /// Adds a value to the result string.
        /// </summary>
        /// <param name="sb">The buffer to which to add.</param>
        /// <param name="result">The value to add to the result string.</param>
        private void FormatResult(StringBuilder sb, double result)
        {
            sb.AppendLine(result.ToString());
        }

        /// <summary>
        /// Adds a value to the result string.
        /// </summary>
        /// <param name="sb">The buffer to which to add.</param>
        /// <param name="result">The value to add to the result string.</param>
        private void FormatResult(StringBuilder sb, XPathNodeIterator result)
        {
            // Output the number of elements in the result set
            sb.AppendLine("{Count=" + result.Count.ToString() + "}");

            // Prepare to write out each element, using our own formatting
            XmlWriterSettings settings = new XmlWriterSettings();
            settings.ConformanceLevel = ConformanceLevel.Fragment;
            settings.Encoding = Encoding.Unicode;
            settings.Indent = true;
            settings.IndentChars = " ";

            foreach (XPathNavigator nav in result)
            {
                // Output the node type
                sb.AppendLine();
                sb.AppendLine("[XPathNodeType." + nav.NodeType.ToString() + "]");
                switch (nav.NodeType)
                {
                    case XPathNodeType.Element:
                    case XPathNodeType.Root:
                        // For element nodes, format them in a pretty way
                        using (MemoryStream stream = new MemoryStream())
                        using (XmlWriter writer = XmlWriter.Create(stream, settings))
                        {
                            nav.WriteSubtree(writer);
                            writer.Flush();
                            stream.Seek(0, SeekOrigin.Begin);
                            sb.Append(Encoding.Unicode.GetString(stream.ToArray()));
                        }
                        break;
                    default:
                        // For all other nodes, just grab the XML
                        sb.Append(nav.OuterXml);
                        break;
                }
                sb.AppendLine();
            }
        }

        /// <summary>
        /// Adds a value to the result string.
        /// </summary>
        /// <param name="sb">The buffer to which to add.</param>
        /// <param name="result">The value to add to the result string.</param>
        private void FormatResult(StringBuilder sb, object result)
        {
            sb.AppendLine("{unknown type} " + result.ToString());
        }

        /// <summary>
        /// Sets the results to display a result value.
        /// </summary>
        /// <param name="expr">The compiled expression.</param>
        /// <param name="result">The result of evaluating the expression.</param>
        /// <param name="parseTime">The time spent parsing the XML.</param>
        /// <param name="compileTime">The time spent compiling the XPath.</param>
        /// <param name="evaluateTime">The time spent evaluating the XPath.</param>
        private void SetResultText(XPathExpression expr, object result, TimeSpan parseTime, TimeSpan compileTime, TimeSpan evaluateTime)
        {
            StringBuilder sb = new StringBuilder();
            
            // Output XPath expression result type and how it maps to the actual (.NET) result type
            sb.Append("[XPathResultType." + expr.ReturnType.ToString() + " -> " + result.GetType().Name + "]: ");

            // Output the result in detail
            string resultString = result as string;
            bool? resultBool = result as bool?;
            double? resultNumber = result as double?;
            XPathNodeIterator resultNodeSet = result as XPathNodeIterator;
            if (resultString != null)
                FormatResult(sb, resultString);
            else if (resultBool.HasValue)
                FormatResult(sb, resultBool.Value);
            else if (resultNumber.HasValue)
                FormatResult(sb, resultNumber.Value);
            else if (resultNodeSet != null)
                FormatResult(sb, resultNodeSet);
            else
                FormatResult(sb, result);
            sb.AppendLine();

            // Output some statistics
            sb.AppendLine("XML Parse time     " + parseTime.ToString());
            sb.AppendLine("XPath Compile time " + compileTime.ToString());
            sb.AppendLine("Evaluation time    " + evaluateTime.ToString());
            sb.Append("End of line.");
            textBoxResults.Text = sb.ToString();
        }

        /// <summary>
        /// Removes an "encoding" declaration from an XML prolog.
        /// </summary>
        /// <param name="xml">The XML to process.</param>
        /// <returns>The XML with the "encoding" removed.</returns>
        private string RemoveExplicitEncoding(string xml)
        {
            // This is necessary because we read/write XML as UTF-16, not UTF-8.

            // When reading the XML from a file, we don't have to mess with the encoding because
            //  it is saved in-memory as UTF-16, and the XML writer will automatically take care
            //  of fixing/removing the encoding declaration if necessary.
            // This function becomes necessary when reading the XML from the clipboard. If the user
            //  pastes example XML (which often has an explicit encoding of UTF-8) into the XML
            //  TextBox, then it is stored as a Unicode string (UTF-16), and reading it as an XML
            //  document will fail with a somewhat obscure error message.

            //Match match = XmlEncodingRegex.Match(xml);
            //if (match.Success)
                //return "<?xml version='1.0'?>" + xml;
            //return xml;
            return xml;
        }

        private void evaluateToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                Stopwatch timer = new Stopwatch();
                timer.Start();

                // Read the XML from the TextBox into an in-memory stream
                using (MemoryStream stream = new MemoryStream(Encoding.Unicode.GetBytes(RemoveExplicitEncoding(textBoxXML.Text)), false))
                {
                    XPathDocument doc = null;
                    try
                    {
                        XmlReaderSettings settings = new XmlReaderSettings();
                        settings.ProhibitDtd = false;
                        using (XmlReader reader = XmlReader.Create(stream, settings))
                        {
                            // Read the actual XML document from the in-memory stream
                            doc = new XPathDocument(reader);
                        }
                    }
                    catch (Exception err)
                    {
                        SetResultText(err, "parsing XML");
                        return;
                    }

                    timer.Stop();
                    TimeSpan parseTime = timer.Elapsed;
                    timer.Start();

                    // Compile the XPath expression
                    XPathNavigator nav = doc.CreateNavigator();
                    XPathExpression expr = null;
                    try
                    {
                        expr = nav.Compile(textBoxXPath.Text);
                    }
                    catch (Exception err)
                    {
                        SetResultText(err, "parsing XPath");
                        return;
                    }

                    timer.Stop();
                    TimeSpan compileTime = timer.Elapsed;
                    timer.Start();

                    // Evaluate the XPath expression
                    object result = null;
                    try
                    {
                        result = nav.Evaluate(expr);
                    }
                    catch (Exception err)
                    {
                        SetResultText(err, "evaluating XPath on XML");
                        return;
                    }

                    timer.Stop();
                    TimeSpan evaluateTime = timer.Elapsed;

                    // Display the results
                    SetResultText(expr, result, parseTime, compileTime, evaluateTime);
                }
            }
            catch (Exception err)
            {
                MessageBox.Show(this, "[" + err.GetType().Name + "]: " + err.Message + err.StackTrace, "Unexpected error");
            }
        }

        private void loadXMLSourceToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (openFileDialog.ShowDialog() != DialogResult.OK)
                return;
            try
            {
                // We use the XmlReader instead of just reading the binary file into memory
                //  to take advantage of the automatic XmlReader support for different encodings.

                // Read xml document from file
                XPathDocument doc;
                XmlReaderSettings settings = new XmlReaderSettings();
                settings.ProhibitDtd = false;
                using (StreamReader stream = new StreamReader(openFileDialog.FileName))
                using (XmlReader reader = XmlReader.Create(stream, settings))
                {
                    doc = new XPathDocument(reader);
                }

                // Write xml document to string
                XmlWriterSettings writeSettings = new XmlWriterSettings();
                writeSettings.ConformanceLevel = ConformanceLevel.Auto;
                writeSettings.Encoding = Encoding.Unicode;
                writeSettings.Indent = true;
                writeSettings.IndentChars = " ";
                using (MemoryStream stream = new MemoryStream())
                using (XmlWriter writer = XmlWriter.Create(stream, writeSettings))
                {
                    doc.CreateNavigator().WriteSubtree(writer);
                    writer.Flush();
                    stream.Position = 0;
                    textBoxXML.Text = Encoding.Unicode.GetString(stream.ToArray());
                }
            }
            catch (Exception err)
            {
                MessageBox.Show(this, "[" + err.GetType().Name + "]: " + err.Message + err.StackTrace, "Unexpected error");
            }
        }

        private void textBoxXPath_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                evaluateToolStripMenuItem_Click(null, null);
            }
        }

        private void textBoxXPath_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
                e.SuppressKeyPress = true;
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
        }
    }
}