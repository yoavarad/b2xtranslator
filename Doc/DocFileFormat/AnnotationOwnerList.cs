using System.Collections.Generic;
using b2xtranslator.StructuredStorage.Reader;
using b2xtranslator.Tools;

namespace b2xtranslator.DocFileFormat
{
    public class AnnotationOwnerList : List<string>
    {
        public AnnotationOwnerList(FileInformationBlock fib, VirtualStream tableStream) : base()
        {
            if (fib.lcbGrpXstAtnOwners == 0)
                return;

            // a range past the end would never be reached (reads at the end don't advance) -> endless loop
            if ((long)fib.fcGrpXstAtnOwners + fib.lcbGrpXstAtnOwners > tableStream.Length)
                throw new ByteParseException("The annotation owner list lies outside the table stream.");

            tableStream.Seek(fib.fcGrpXstAtnOwners, System.IO.SeekOrigin.Begin);

            while (tableStream.Position < (fib.fcGrpXstAtnOwners + fib.lcbGrpXstAtnOwners))
            {
                this.Add(Utils.ReadXst(tableStream));
            }
        }
    }
}
