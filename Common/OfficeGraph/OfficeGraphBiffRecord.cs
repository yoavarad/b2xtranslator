

using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using b2xtranslator.StructuredStorage.Reader;

namespace b2xtranslator.OfficeGraph
{
    public abstract class OfficeGraphBiffRecord
    {
        GraphRecordNumber _id;
        uint _length;
        long _offset;

        /// <summary>
        /// Ctor 
        /// </summary>
        /// <param name="reader">Streamreader</param>
        /// <param name="id">Record ID - Recordtype</param>
        /// <param name="length">The recordlegth</param>
        public OfficeGraphBiffRecord(IStreamReader reader, GraphRecordNumber id, uint length)
        {
            this._reader = reader;
            this._offset = this._reader.BaseStream.Position;

            this._id = id;
            this._length = length;
        }

        private static Dictionary<ushort, Type> TypeToRecordClassMapping = new Dictionary<ushort, Type>();

        /// <summary>
        /// Constructor delegates per record id, built once at registration (null if the class has no matching constructor).
        /// </summary>
        private static Dictionary<ushort, Func<IStreamReader, GraphRecordNumber, ushort, OfficeGraphBiffRecord>> TypeToRecordFactoryMapping =
            new Dictionary<ushort, Func<IStreamReader, GraphRecordNumber, ushort, OfficeGraphBiffRecord>>();

        private static Func<IStreamReader, GraphRecordNumber, ushort, OfficeGraphBiffRecord> CreateFactory(Type cls)
        {
            var constructor = cls.GetConstructor(
                new Type[] { typeof(IStreamReader), typeof(GraphRecordNumber), typeof(ushort) }
                );

            if (constructor == null)
                return null;

            var parameters = new[] {
                Expression.Parameter(typeof(IStreamReader)), Expression.Parameter(typeof(GraphRecordNumber)), Expression.Parameter(typeof(ushort)) };

            return Expression.Lambda<Func<IStreamReader, GraphRecordNumber, ushort, OfficeGraphBiffRecord>>(
                Expression.Convert(Expression.New(constructor, parameters), typeof(OfficeGraphBiffRecord)), parameters).Compile();
        }

        static OfficeGraphBiffRecord()
        {
            UpdateTypeToRecordClassMapping(
                Assembly.GetExecutingAssembly(),
                typeof(OfficeGraphBiffRecord).Namespace);
        }

        public static void UpdateTypeToRecordClassMapping(Assembly assembly, string ns)
        {
            foreach (var t in assembly.GetTypes())
            {
                if (ns == null || t.Namespace == ns)
                {
                    var attrs = t.GetCustomAttributes(typeof(OfficeGraphBiffRecordAttribute), false);

                    OfficeGraphBiffRecordAttribute attr = null;

                    if (attrs.Length > 0)
                        attr = attrs[0] as OfficeGraphBiffRecordAttribute;

                    if (attr != null)
                    {
                        // Add the type codes of the array
                        foreach (ushort typeCode in attr.TypeCodes)
                        {
                            if (TypeToRecordClassMapping.ContainsKey(typeCode))
                            {
                                throw new Exception(string.Format(
                                    "Tried to register TypeCode {0} to {1}, but it is already registered to {2}",
                                    typeCode, t, TypeToRecordClassMapping[typeCode]));
                            }
                            TypeToRecordClassMapping.Add(typeCode, t);
                            TypeToRecordFactoryMapping.Add(typeCode, CreateFactory(t));
                        }
                    }
                }
            }
        }

        [Obsolete("Use OfficeGraphBiffRecordSequence.GetNextRecordNumber")]
        public static GraphRecordNumber GetNextRecordNumber(IStreamReader reader)
        {
            // read next id
            var nextRecord = (GraphRecordNumber)reader.ReadUInt16();

            // seek back
            reader.BaseStream.Seek(-sizeof(ushort), System.IO.SeekOrigin.Current);

            return nextRecord;
        }

        [Obsolete("Use OfficeGraphBiffRecordSequence.ReadRecord")]
        public static OfficeGraphBiffRecord ReadRecord(IStreamReader reader)
        {
            OfficeGraphBiffRecord result = null;
            try
            {
                ushort id = reader.ReadUInt16();
                ushort size = reader.ReadUInt16();
                Func<IStreamReader, GraphRecordNumber, ushort, OfficeGraphBiffRecord> factory;
                if (TypeToRecordFactoryMapping.TryGetValue(id, out factory))
                {
                    // a null factory (no matching constructor) throws NullReferenceException, as the reflection call did
                    result = factory(reader, (GraphRecordNumber)id, size);
                }
                else
                {
                    result = new UnknownGraphRecord(reader, id, size);
                }

                return result;
            }
            catch (OutOfMemoryException e)
            {
                throw new Exception("Invalid record", e);
            }
        }

        public GraphRecordNumber Id
        {
            get { return this._id; }
        }

        public uint Length
        {
            get { return this._length; }
        }

        public long Offset
        {
            get { return this._offset; }
        }

        IStreamReader _reader;
        public IStreamReader Reader
        {
            get { return this._reader; }
            set { this._reader = value; }
        }
    }
}
