using System;
using System.Collections.Generic;
using b2xtranslator.StructuredStorage.Reader;

namespace b2xtranslator.DocFileFormat
{
    public class Plex<T>
    {
        protected const int CP_LENGTH = 4;

        public List<int> CharacterPositions;
        public List<T> Elements;

        public Plex(int structureLength, VirtualStream tableStream, uint fc, uint lcb)
        {
            tableStream.Seek((long)fc, System.IO.SeekOrigin.Begin);
            var reader = new VirtualStreamReader(tableStream);

            int n = 0;
            if (structureLength > 0)
            {
                //this PLEX contains CPs and Elements
                n = ((int)lcb - CP_LENGTH) / (structureLength + CP_LENGTH);
            }
            else
            {
                //this PLEX only contains CPs
                n = ((int)lcb - CP_LENGTH) / CP_LENGTH;
            }

            //read the n + 1 CPs
            this.CharacterPositions = new List<int>();
            for (int i = 0; i < n + 1; i++)
            {
                this.CharacterPositions.Add(reader.ReadInt32());
            }

            //read the n structs
            this.Elements = new List<T>();
            var genericType = typeof(T);
            if (genericType == typeof(short))
            {
                this.Elements = new List<T>();
                for (int i = 0; i < n; i++)
                {
                    short value = reader.ReadInt16();
                    var genericValue = (T)Convert.ChangeType(value, typeof(T));
                    this.Elements.Add(genericValue);
                }
            }
            else if (structureLength > 0)
            {
                //look up the constructor once, not per element
                var constructor = n > 0 ? genericType.GetConstructor(new Type[] { typeof(VirtualStreamReader), typeof(int) }) : null;
                for (int i = 0; i < n; i++)
                {
                    this.Elements.Add((T)constructor.Invoke(new object[] { reader, structureLength }));
                }
            }

        }

        /// <summary>
        /// Retruns the struct that matches the given character position.
        /// </summary>
        /// <param name="cp">The character position</param>
        /// <returns>The matching struct</returns>
        public T GetStruct(int cp)
        {
            //CPs are sorted; binary search, then step back to the first duplicate (matches the former linear scan)
            int index = this.CharacterPositions.BinarySearch(cp);
            while (index > 0 && this.CharacterPositions[index - 1] == cp)
            {
                index--;
            }

            if (index >= 0 && index < this.Elements.Count)
            {
                return this.Elements[index];
            }
            else
            {
                return default(T);
            }
        }
    }
}
