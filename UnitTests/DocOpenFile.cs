using b2xtranslator.DocFileFormat;
using b2xtranslator.OpenXmlLib.WordprocessingML;
using b2xtranslator.StructuredStorage.Reader;
using Microsoft.Office.Interop.Word;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml;
using static b2xtranslator.OpenXmlLib.OpenXmlPackage;

namespace UnitTests
{
    [TestFixture]
    public class DocOpenFile
    {
        // No field initializer: touching this field from the constructor would
        // load the Word interop types even when Office is not installed.
        Application word2007;
        List<FileInfo> files;
        object confirmConversions = Type.Missing;
        object readOnly = true;
        object addToRecentFiles = Type.Missing;
        object passwordDocument = Type.Missing;
        object passwordTemplate = Type.Missing;
        object revert = Type.Missing;
        object writePasswordDocument = Type.Missing;
        object writePasswordTemplate = Type.Missing;
        object format = Type.Missing;
        object encoding = Type.Missing;
        object visible = Type.Missing;
        object openConflictDocument = Type.Missing;
        object openAndRepair = Type.Missing;
        object documentDirection = Type.Missing;
        object noEncodingDialog = Type.Missing;

        object saveChanges = false;
        object originalFormat = false;
        object routeDocument = false;

        const string RequiresWord = "RequiresWord";
        string wordUnavailableReason = null;


        [OneTimeSetUp]
        public void SetUp()
        {
            //read the config
            var fs = new FileStream("Config.xml", FileMode.Open);
            var config = new XmlDocument();
            config.Load(fs);
            fs.Close();

            //read the inputfiles
            this.files = new List<FileInfo>();
            foreach (XmlNode fileNode in config.SelectNodes("input-files/file"))
            {
                this.files.Add(new FileInfo(fileNode.Attributes["path"].Value));
            }

            //start the application; the Word-comparison tests are ignored when
            //Word / the Office 2007 interop assemblies are not installed
            try
            {
                StartWord();
            }
            catch (Exception ex) when (ex is FileNotFoundException || ex is FileLoadException || ex is COMException || ex is PlatformNotSupportedException)
            {
                this.wordUnavailableReason = "Microsoft Word (Office 2007 interop) is not available: " + ex.Message;
            }
        }


        [OneTimeTearDown]
        public void TearDown()
        {
            if (this.wordUnavailableReason == null)
                QuitWord();
        }

        // Ignore Word-dependent tests before NUnit invokes (and JIT-compiles)
        // them, since their bodies reference the Word interop types.
        [SetUp]
        public void RequireWordIfNeeded()
        {
            if (this.wordUnavailableReason != null
                && TestContext.CurrentContext.Test.Properties["Category"].Contains(RequiresWord))
            {
                Assert.Ignore(this.wordUnavailableReason);
            }
        }

        // Kept in separate non-inlined methods so a missing interop assembly
        // surfaces as a catchable exception at the call site instead of
        // failing JIT compilation of the NUnit setup/teardown methods.
        [MethodImpl(MethodImplOptions.NoInlining)]
        void StartWord()
        {
            this.word2007 = new Application();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        void QuitWord()
        {
            this.word2007.Quit(
                ref this.saveChanges,
                ref this.originalFormat,
                ref this.routeDocument);
        }


        /// <summary>
        /// Tests if the inputfile is parsable
        /// </summary>
        [Test]
        public void ParseabilityTest()
        {
            foreach (var inputFile in this.files)
            {
                var reader = new StructuredStorageReader(inputFile.FullName);
                var doc = new WordDocument(reader);
            }
        }


        [Test]
        public void SaveTest()
        {
            foreach (var inputFile in this.files)
            {
                var reader = new StructuredStorageReader(inputFile.FullName);
                var doc = new WordDocument(reader);

                // Create the output DOCX
                var docx = WordprocessingDocument.Create(inputFile.FullName + "x", DocumentType.Document);
                b2xtranslator.WordprocessingMLMapping.Converter.Convert(doc, docx);
            }
        }

        [Test]
        [Category(RequiresWord)]
        public void PropertiesTest()
        {
            foreach (var inputFile in this.files)
            {
                var omDoc = LoadDocument(inputFile.FullName);
                var dffDoc = new WordDocument(new StructuredStorageReader(inputFile.FullName));

                string dffRevisionNumber = dffDoc.DocumentProperties.nRevision.ToString();
                string omRevisionNumber = (string)getDocumentProperty(omDoc, "Revision number");

                var omCreationDate = (DateTime?)getDocumentProperty(omDoc, "Creation date");
                var dffCreationDate = dffDoc.DocumentProperties.dttmCreated.ToDateTime();

                var omLastPrintedDate = (DateTime?)getDocumentProperty(omDoc, "Last print date");
                var dffLastPrintedDate = dffDoc.DocumentProperties.dttmLastPrint.ToDateTime();

                omDoc.Close(ref this.saveChanges, ref this.originalFormat, ref this.routeDocument);

                Assert.AreEqual(omRevisionNumber, dffRevisionNumber, $"Invalid revision number for {inputFile.FullName}");

                if ((omCreationDate?.Year ?? 1601) != 1601)
                    Assert.AreEqual(omCreationDate.Value, dffCreationDate, $"Invalid creation date for {inputFile.FullName}");

                if ((omLastPrintedDate?.Year ?? 1601) != 1601)
                    Assert.AreEqual(omLastPrintedDate.Value, dffLastPrintedDate, $"Invalid print date for {inputFile.FullName}");
            }
        }


        /// <summary>
        /// 
        /// </summary>
        public void CharactersTest()
        {
            foreach (var inputFile in this.files)
            {
                var omDoc = LoadDocument(inputFile.FullName);
                var dffDoc = new WordDocument(new StructuredStorageReader(inputFile.FullName));
                omDoc.Fields.ToggleShowCodes();

                var omText = new StringBuilder();
                var omMainText = omDoc.StoryRanges[WdStoryType.wdMainTextStory].Text.ToCharArray();
                foreach (char c in omMainText)
                    if ((int)c > 0x20)
                        omText.Append(c);

                var dffText = new StringBuilder();
                var dffMainText = dffDoc.Text.GetRange(0, dffDoc.FIB.ccpText);
                foreach (char c in dffMainText)
                    if ((int)c > 0x20)
                        dffText.Append(c);

                Assert.AreEqual(omText.ToString(), dffText.ToString(), $"Invalid characters for {inputFile.FullName}");
            }
        }


        /// <summary>
        /// Tests the count of bookmarks in the documents.
        /// Also tests the start and the end position a randomly selected bookmark.
        /// </summary>
        [Test]
        [Category(RequiresWord)]
        public void BookmarksTest()
        {
            foreach (var inputFile in this.files)
            {
                var omDoc = LoadDocument(inputFile.FullName);
                var dffDoc = new WordDocument(new StructuredStorageReader(inputFile.FullName));
                omDoc.Bookmarks.ShowHidden = true;

                int omBookmarkCount = omDoc.Bookmarks.Count;
                int dffBookmarkCount = dffDoc.BookmarkNames.Strings.Count;
                int omBookmarkStart = 0;
                int dffBookmarkStart = 0;
                int omBookmarkEnd = 0;
                int dffBookmarkEnd = 0;

                if (omBookmarkCount > 0 && dffBookmarkCount > 0)
                {
                    //generate a randomly selected bookmark
                    var rand = new Random();
                    object omIndex = rand.Next(0, dffBookmarkCount);

                    //get the index's bookmark
                    var omBookmark = omDoc.Bookmarks.get_Item(ref omIndex);
                    omBookmarkStart = omBookmark.Start;
                    omBookmarkEnd = omBookmark.End;

                    //get the bookmark with the same name from DFF
                    int dffIndex = 0;
                    for (int i = 0; i < dffDoc.BookmarkNames.Strings.Count; i++)
                        if (dffDoc.BookmarkNames.Strings[i] == omBookmark.Name)
                        {
                            dffIndex = i;
                            break;
                        }

                    dffBookmarkStart = dffDoc.BookmarkStartPlex.CharacterPositions[dffIndex];
                    dffBookmarkEnd = dffDoc.BookmarkEndPlex.CharacterPositions[dffIndex];
                }

                omDoc.Close(ref this.saveChanges, ref this.originalFormat, ref this.routeDocument);

                //compare bookmark count
                Assert.AreEqual(omBookmarkCount, dffBookmarkCount, $"Invalid count of bookmarks for {inputFile.FullName}");

                //compare bookmark start
                Assert.AreEqual(omBookmarkStart, dffBookmarkStart, $"Invalid bookmark start for {inputFile.FullName}");

                //compare bookmark end
                Assert.AreEqual(omBookmarkEnd, dffBookmarkEnd, $"Invalid bookmark end for {inputFile.FullName}");
            }
        }


        /// <summary>
        /// Tests the count of of comments in the documents.
        /// Also compares the author of the first comment.
        /// </summary>
        [Test]
        [Category(RequiresWord)]
        public void CommentsTest()
        {
            foreach (var inputFile in this.files)
            {
                var omDoc = LoadDocument(inputFile.FullName);
                var dffDoc = new WordDocument(new StructuredStorageReader(inputFile.FullName));

                int dffCommentCount = dffDoc.AnnotationsReferencePlex.Elements.Count;
                int omCommentCount = omDoc.Comments.Count;
                string omFirstCommentInitial = "";
                string omFirstCommentAuthor = "";
                string dffFirstCommentInitial = "";
                string dffFirstCommentAuthor = "";

                if (dffCommentCount > 0 && omCommentCount > 0)
                {
                    var omFirstComment = omDoc.Comments[1];
                    var dffFirstComment = dffDoc.AnnotationsReferencePlex.Elements[0];

                    omFirstCommentInitial = omFirstComment.Initial;
                    omFirstCommentAuthor = omFirstComment.Author;

                    dffFirstCommentInitial = dffFirstComment.UserInitials;
                    dffFirstCommentAuthor = dffDoc.AnnotationOwners[dffFirstComment.AuthorIndex];
                }

                omDoc.Close(ref this.saveChanges, ref this.originalFormat, ref this.routeDocument);

                //compare comment count
                Assert.AreEqual(omCommentCount, dffCommentCount, $"Invalid comment count for {inputFile.FullName}");

                //compare initials
                Assert.AreEqual(omFirstCommentInitial, dffFirstCommentInitial, $"Invalid first comment for {inputFile.FullName}");

                //compate the author names
                Assert.AreEqual(omFirstCommentAuthor, dffFirstCommentAuthor, $"Invalid author name for {inputFile.FullName}");
            }
        }

        Document LoadDocument(object filename)
        {
            return this.word2007.Documents.Open(
                ref filename,
                ref this.confirmConversions,
                ref this.readOnly,
                ref this.addToRecentFiles,
                ref this.passwordDocument,
                ref this.passwordTemplate,
                ref this.revert,
                ref this.writePasswordDocument,
                ref this.writePasswordTemplate,
                ref this.format,
                ref this.encoding,
                ref this.visible,
                ref this.openConflictDocument,
                ref this.openAndRepair,
                ref this.documentDirection,
                ref this.noEncodingDialog);
        }

        object getDocumentProperty(Document document, string propertyName)
        {
            object propertyValue = null;
            try
            {
                object builtInProperties = document.BuiltInDocumentProperties;
                var builtInPropertiesType = builtInProperties.GetType();

                object property = builtInPropertiesType.InvokeMember("Item", BindingFlags.GetProperty, null, builtInProperties, new object[] { propertyName });
                var propertyType = property.GetType();

                propertyValue = propertyType.InvokeMember("Value", BindingFlags.GetProperty, null, property, new object[] { });
            }
            catch (TargetInvocationException)
            {
            }

            return propertyValue;
        }
    }
}