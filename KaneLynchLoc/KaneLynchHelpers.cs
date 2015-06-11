using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Xml;

namespace KaneLynchLoc
{
    ///////////////////////////////////////////////////////////////////////
    // my rationale for inheriting disposable so was i can store
    // temporary values with the 'using' syntax

    public struct NumberStore : IDisposable
    {
        public int Value;

        public void Dispose() { }

        public NumberStore Read(BinaryReader br)
        {
            Value = br.ReadInt32();
            return this;
        }

        public NumberStore Write(BinaryWriter bw)
        {
            bw.Write(Value);
            return this;
        }
    }

    ///////////////////////////////////////////////////////////////////////

    public struct StringStore : IDisposable
    {
        public string Value;

        private int _ValueSize;
        public int ReadSize { get { return _ValueSize; } }
        public int WriteSize { get { return _ValueSize; } }

        public void Dispose() { }

        public StringStore Read(BinaryReader br)
        {
            var raw = new List<byte>();

            // this is reading a null-terminated string
            for (byte val = br.ReadByte(); val != 0; val = br.ReadByte())
            {
                raw.Add(val);
            }

            Value = Encoding.UTF8.GetString(raw.ToArray());
            _ValueSize = raw.Count + 1;

            return this;
        }

        public StringStore Write(BinaryWriter bw)
        {
            byte[] raw = Encoding.UTF8.GetBytes(Value);

            bw.Write(raw, 0, raw.Length);
            bw.Write((byte)0);

            _ValueSize = raw.Length + 1;

            return this;
        }
    }
    
    ///////////////////////////////////////////////////////////////////////

    enum CInternalType
    {
        Undefined,
        Empty,
        Enum,
        List,
        Number,
        String
    }

    ///////////////////////////////////////////////////////////////////////

    class CType
    {
        public CInternalType CoreType { get; private set; }
        public string Name { get; private set; }
        public List<int> Tail;

        public CType(CInternalType _type, string _name)
        {
            CoreType = _type;
            Name = _name;

            Tail = new List<int>();
        }

        public CType()
        {
            CoreType = CInternalType.Undefined;
            Name = "";

            Tail = new List<int>();
        }

        protected virtual void ExportXmlImpl(ref XmlWriter xml) { }

        public void ExportXml(ref XmlWriter xml)
        {
            xml.WriteStartElement("node");
            xml.WriteAttributeString("name", Name);
            xml.WriteAttributeString("type", CoreType.ToString());

            // tail must be joined because xml doesn't support duplicate attributes
            if (Tail.Count > 0)
            {
                xml.WriteAttributeString("metadata", string.Join(",", Tail));
            }

            ExportXmlImpl(ref xml);

            xml.WriteFullEndElement();
        }
    }

    ///////////////////////////////////////////////////////////////////////

    class CTypeEmpty : CType
    {
        public CTypeEmpty(string _name)
            : base(CInternalType.Empty, _name) {}
    }

    ///////////////////////////////////////////////////////////////////////

    class CTypeEnum : CType
    {
        public string SValue;
        public int IValue;

        public CTypeEnum(string _name)
            : base(CInternalType.Enum, _name)
        {
            SValue = "";
            IValue = 0;
        }

        protected override void ExportXmlImpl(ref XmlWriter xml)
        {
            // note that some enum strings which are horribly large (credits_credits)
            // so we can't fit the string value into an attribute nicely
            // instead we can flag using attributes this is an enum and write the string as a string! ta-dah
            xml.WriteAttributeString("enum", IValue.ToString());
            xml.WriteString(SValue);

            // most enum values are 0 anyway...
        }
    }

    ///////////////////////////////////////////////////////////////////////

    class CTypeChild : CType
    {
        public List<CType> Children;

        public CTypeChild(string _name)
            : base(CInternalType.List, _name)
        {
            Children = new List<CType>();
        }

        protected override void ExportXmlImpl(ref XmlWriter xml)
        {
            foreach (CType child in Children)
            {
                child.ExportXml(ref xml);
            }
        }

        public string ExportXmlRoot()
        {
            // just noticed how ugly this is looking..

            XmlWriterSettings xml_settings = new XmlWriterSettings();
            xml_settings.Indent = true;

            StringBuilder log = new StringBuilder();

            XmlWriter xml = XmlWriter.Create(log, xml_settings);

            xml.WriteStartElement("dummy");

            foreach (CType child in Children)
            {
                child.ExportXml(ref xml);
            }

            xml.WriteFullEndElement();

            // important step, right here:
            xml.Flush();

            return log.ToString();
        }
    }

    ///////////////////////////////////////////////////////////////////////

    class CTypeNumber : CType
    {
        public int IValue;
        public bool HasValue;

        public CTypeNumber(string _name)
            : base(CInternalType.Number, _name)
        {
            IValue = 0;
            HasValue = false;
        }

        protected override void ExportXmlImpl(ref XmlWriter xml)
        {
            // we must always export with this attribute to identify it is, in fact, a number (not empty)
            xml.WriteAttributeString("value", HasValue ? IValue.ToString() : "");
        }
    }

    ///////////////////////////////////////////////////////////////////////

    class CTypeString : CType
    {
        public string SValue;

        public CTypeString(string _name)
            : base(CInternalType.String, _name)
        {
            SValue = "";
        }

        protected override void ExportXmlImpl(ref XmlWriter xml)
        {
            // string data exists between nodes
            xml.WriteString(SValue);
        }
    }

    ///////////////////////////////////////////////////////////////////////
}
