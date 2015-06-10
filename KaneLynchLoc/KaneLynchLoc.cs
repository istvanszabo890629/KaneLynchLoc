using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Xml;
using System.Collections; // for stack

namespace KaneLynchLoc
{
    class KaneLynchLoc
    {
        ///////////////////////////////////////////////////////////////////////

        CTypeChild rootChild;

        ///////////////////////////////////////////////////////////////////////

        public KaneLynchLoc()
        {
            rootChild = null;
        }

        ///////////////////////////////////////////////////////////////////////

        enum Types : byte
        {
            Empty = 0x8,
            Enum = 0x9,
            List = 0x10,
            Number = 0x28,
            String = 0x29
        }

        ///////////////////////////////////////////////////////////////////////

        int ScanChild(BinaryReader br, int max_len, ref CTypeChild root)
        {
            int cur_len = 0;
            string name;

            using (StringStore tmp = new StringStore().Read(br))
            {
                name = tmp.Value;
                cur_len += tmp.ReadSize;
            }

            CType last_node = new CType();

            if (cur_len < max_len)
            {
                Types type = (Types)br.ReadByte();
                cur_len += 1;

                switch (type)
                {
                    case Types.Empty:
                        {
                            CTypeEmpty me = new CTypeEmpty(name);

                            last_node = me;

                            break;
                        }

                    case Types.Enum:
                        {
                            string val;

                            using (StringStore tmp = new StringStore().Read(br))
                            {
                                val = tmp.Value;
                                cur_len += tmp.ReadSize;
                            }

                            int ival;

                            using (NumberStore tmp = new NumberStore().Read(br))
                            {
                                ival = tmp.Value;
                                cur_len += sizeof(int);
                            }

                            CTypeEnum me = new CTypeEnum(name);
                            me.SValue = val;
                            me.IValue = ival;

                            last_node = me;

                            break;
                        }

                    case Types.String:
                        {
                            string val;
                            using (StringStore tmp = new StringStore().Read(br))
                            {
                                val = tmp.Value;
                                cur_len += tmp.ReadSize;
                            }

                            CTypeString me = new CTypeString(name);
                            me.SValue = val;

                            last_node = me;

                            break;
                        }

                    case Types.Number:
                        {
                            CTypeNumber me = new CTypeNumber(name);

                            // required. some number nodes end before they have any data. yup.
                            if (cur_len != max_len)
                            {
                                int ival;
                                using (NumberStore tmp = new NumberStore().Read(br))
                                {
                                    ival = tmp.Value;
                                    cur_len += sizeof(int);
                                }

                                me.HasValue = true;
                                me.IValue = ival;
                            }

                            last_node = me;

                            break;
                        }

                    case Types.List:
                        {
                            byte Count = br.ReadByte();
                            cur_len += 1;

                            CTypeChild me = new CTypeChild(name);
                            
                            if (Count > 0)
                            {
                                // multiple child nodes have a list of lengths
                                if (Count > 1)
                                {
                                    int real_count = Count - 1;
                                    var items = new List<int>();

                                    for (int i = 0; i < real_count; ++i)
                                    {
                                        items.Add(br.ReadInt32());
                                    }

                                    cur_len += real_count * sizeof(int);

                                    int last = 0;
                                    for (int i = 0; i < real_count; ++i)
                                    {
                                        int child_len = ScanChild(br, items[i] - last, ref me);
                                        cur_len += child_len;
                                        last = items[i];
                                    }
                                }

                                int ret_len = ScanChild(br, max_len - cur_len, ref me);
                                cur_len += ret_len;
                            }

                            last_node = me;

                            break;
                        }

                    default:
                        break;
                }
            }

            // oh, there is also random tail data
            // we store this as 'tail' data, but in xml this is 'metadata'

            int tail_length = max_len - cur_len;

            if (tail_length != 0)
            {
                if ((tail_length & 3) != 0)
                {
                    throw new Exception("Unexpected tail");
                }

                int tail_count = tail_length / sizeof(int);
                for (int i = 0; i < tail_count; ++i)
                {
                    int ival = br.ReadInt32();

                    last_node.Tail.Add(ival);
                }

                cur_len = max_len;
            }

            root.Children.Add(last_node);

            return cur_len;
        }

        ///////////////////////////////////////////////////////////////////////

        public bool ReadXml(string file_name)
        {
            bool valid = true;

            if( rootChild != null )
            {
                // trash the existing loaded data
                rootChild = null;
            }

            valid &= File.Exists(file_name);

            if (valid)
            {
                StreamReader src = new StreamReader(file_name);

                XmlReader xml = XmlReader.Create(src);

                // we need to track the parent node stack as child nodes only track their own children
                var child_stack = new Stack<CTypeChild>();

                // dummy root
                rootChild = new CTypeChild("_root");
                child_stack.Push(rootChild);

                // abtract node so we can determine the true node type at a later stage
                CType last_node = null;

                while (xml.Read())
                {
                    // good info on msdn about this - https://msdn.microsoft.com/en-us/library/cc189056%28v=vs.95%29.aspx

                    switch (xml.NodeType)
                    {
                        case XmlNodeType.Element:
                            // all our nodes are named node for fun. we figure out the type implicitly (:
                            if (xml.Name != "node")
                            {
                                throw new Exception("Horribly wrong!");
                            }

                            // deal with unhandled last node. as we've started a new element, it has children now
                            if (last_node != null)
                            {
                                if (last_node.CoreType != CInternalType.List)
                                {
                                    // should only throw when a node didn't end with an EndElement
                                    // which suggests we failed parsing midway through
                                    throw new Exception("Parsing elements failed");
                                }

                                // append this new node to the last one
                                child_stack.First().Children.Add(last_node);

                                // and begin anew
                                child_stack.Push(last_node as CTypeChild);

                                // dealt with.
                                last_node = null;
                            }

                            // we store this as an attribute because some names have spaces, which breaks xml node names
                            string name = xml.GetAttribute("name");

                            if (name == null)
                            {
                                throw new Exception("Wrong data (no name attribute)");
                            }

                            string type = xml.GetAttribute("type");

                            if (type == null)
                            {
                                throw new Exception("Wrong data (no type attribute)");
                            }

                            switch (type)
                            {
                                case "Empty":
                                    {
                                        last_node = new CTypeEmpty(name);
                                    }
                                    break;

                                case "Enum":
                                    {
                                        var tmp = new CTypeEnum(name);

                                        string enum_val = xml.GetAttribute("enum");

                                        if (enum_val == null) throw new Exception("Bad enum");
                                        tmp.IValue = Convert.ToInt32(enum_val);

                                        last_node = tmp;
                                    }
                                    break;

                                case "List":
                                    {
                                        last_node = new CTypeChild(name);
                                    }
                                    break;

                                case "Number":
                                    {
                                        var tmp = new CTypeNumber(name);

                                        string int_val = xml.GetAttribute("value");

                                        if (int_val == null) throw new Exception("Bad number");

                                        if (int_val != "") // determines if there is a value or not (there may not be..)
                                        {
                                            tmp.IValue = Convert.ToInt32(int_val);
                                            tmp.HasValue = true;
                                        }

                                        last_node = tmp;
                                    }
                                    break;

                                case "String":
                                    {
                                        last_node = new CTypeString(name);
                                        // no actual string until we parse that part of the xml
                                    }
                                    break;

                                default:
                                    throw new Exception("Unhandled node type");

                            }

                            // get metadata and copy to tail if it exists
                            string metadata = xml.GetAttribute("metadata");

                            if (metadata != null) // check we have any tail data
                            {
                                // whoops, silly bug where new char[','] allocated however many characters the comma ascii is!
                                string[] _metadata = metadata.Split(new char[] { ',' });

                                foreach (string str in _metadata)
                                {
                                    last_node.Tail.Add(Convert.ToInt32(str));
                                }
                            }

                            break;
                        case XmlNodeType.Text:

                            string string_val = xml.Value;

                            switch (last_node.CoreType)
                            {
                                case CInternalType.Enum:
                                    {
                                        // just assign the string value to ctypenum
                                        var last = last_node as CTypeEnum;
                                        last.SValue = string_val;
                                    }
                                    break;

                                case CInternalType.String:
                                    {
                                        // just assign the string to the ctypestring
                                        var last = last_node as CTypeString;
                                        last.SValue = string_val;
                                    }
                                    break;

                                default:
                                    {
                                        throw new Exception("Unknown CoreType with string data");
                                    }
                            }

                            break;

                        case XmlNodeType.EndElement:

                            // all elements must end, but last_node may not always be an item

                            // must be a child which has just ended??
                            if (last_node == null)
                            {
                                // parent node ends
                                child_stack.Pop();
                            }
                            else
                            {
                                //if( last_node.CoreType == CInternalType.List )
                                //{
                                //    throw new Exception("Ending an unknown element which isn't a child node. Report this file!");
                                //}

                                // add this as a child of the last (well, first on the stack) parent
                                child_stack.Peek().Children.Add(last_node);
                                last_node = null;
                            }

                            break;
                    }
                }

#if DEBUG
                if (last_node != null)
                {
                    throw new Exception("Something went very wrong!");
                }
#endif

                src.Close();
            }

            return valid;
        }

        ///////////////////////////////////////////////////////////////////////

        public bool ReadLoc(string file_name)
        {
            bool valid = true;

            if (rootChild != null)
            {
                // trash the existing loaded data
                rootChild = null;
            }

            valid &= File.Exists(file_name);

            if (valid)
            {
                Stream fh = File.OpenRead(file_name);
                BinaryReader br = new BinaryReader(fh);

                int root_count = (int)br.ReadByte();
                var root_sizes = new List<int>();

                int root_offset_count = root_count - 1;
                int first_root_offset = 1 + (root_offset_count * sizeof(int));
                int last_root_offset = first_root_offset;

                if (root_offset_count != 0)
                {
                    for (int i = 0; i < root_offset_count; ++i)
                    {
                        int size = br.ReadInt32();

                        root_sizes.Add(size);

                        // May be calculated wrong. No samples have root_offset_count > 1
                        last_root_offset += size;
                    }
                }

                root_sizes.Add((int)br.BaseStream.Length - last_root_offset);

                rootChild = new CTypeChild("_root");

                for (int i = 0; i < root_count; ++i)
                {
                    ScanChild(br, root_sizes[i], ref rootChild);
                }

                valid &= (br.BaseStream.Position == br.BaseStream.Length);

                fh.Close();
            }

            return valid;
        }

        ///////////////////////////////////////////////////////////////////////

        public bool WriteXml(string file_name)
        {
            bool valid = true;

            valid &= (rootChild != null);

            if (valid)
            {
                StreamWriter stream = File.CreateText(file_name);

                valid &= stream.BaseStream.CanWrite;

                if (valid)
                {
                    string xml = "";
                    xml += rootChild.ExportXmlRoot();

                    stream.Write(xml);
                }

                stream.Close();
            }

            return valid;
        }

        ///////////////////////////////////////////////////////////////////////

        private int WriteChild(BinaryWriter bw, CType child)
        {
            int len = 0;

            // name is agnostic
            var namestr = new StringStore();
            namestr.Value = child.Name;

            namestr.Write(bw);

            // WHAT ABOUT LENGTH? ANY SPECIAL CHARACTERS THIS WILL FAIL. todo.
            len += namestr.WriteSize;

            switch( child.CoreType )
            {
                case CInternalType.Empty:
                    bw.Write((byte)Types.Empty);
                    len += 1;
                    break;

                case CInternalType.Undefined:
                    throw new Exception("All wrong.");
                   
                case CInternalType.String:

                    bw.Write((byte)Types.String);
                    len += 1;

                    var s_val = new StringStore();
                    s_val.Value = (child as CTypeString).SValue;
                    s_val.Write(bw);
                    len += s_val.WriteSize;

                    break;

                case CInternalType.Number:

                    bw.Write((byte)Types.Number);
                    len += 1;

                    if( (child as CTypeNumber).HasValue )
                    {
                        var n_val = new NumberStore();
                        n_val.Value = (child as CTypeNumber).IValue;
                        n_val.Write(bw);
                        len += sizeof(int);
                    }

                    break;

                case CInternalType.Enum:

                    bw.Write((byte)Types.Enum);
                    len += 1;

                    var e_string = new StringStore();
                    e_string.Value = (child as CTypeEnum).SValue;
                    e_string.Write(bw);
                    len += e_string.WriteSize;

                    var e_number = new NumberStore();
                    e_number.Value = (child as CTypeEnum).IValue;
                    e_number.Write(bw);
                    len += sizeof(int);

                    break;

                case CInternalType.List:

                    bw.Write((byte)Types.List);
                    len += 1;

                    // nastier.

                    var child_node = child as CTypeChild;

                    int count = child_node.Children.Count;

                    bw.Write((byte)(count & 0xFF));
                    len += 1;
                    
                    if( count > 0 )
                    {
                        if( count > 1 )
                        {
                            int hdr_offset = (int)bw.BaseStream.Length;

                            // we need to revisit this..
                            int real_count = count - 1;
                            for (int i = 0; i < real_count; ++i)
                            {
                                bw.Write((int)0xF0F0F0F);
                            }

                            len += real_count * sizeof(int);

                            var child_lengths = new List<int>();

                            // this is no longer the 'real count'
                            for(int i=0; i < real_count; ++i)
                            {
                                int child_length = WriteChild(bw, child_node.Children[i]);
                                child_lengths.Add(child_length);

                                len += child_length;
                            }

                            // revist all data length
                            int tail_offset = (int)bw.BaseStream.Length;
                            bw.Seek(hdr_offset, SeekOrigin.Begin);

                            int sum = 0;
                            foreach( int cl in child_lengths )
                            {
                                sum += cl;
                                bw.Write(sum);
                            }

                            bw.Seek(tail_offset, SeekOrigin.Begin);
                        }

                        len += WriteChild(bw, child_node.Children.Last());
                    }

                    break;
            }

            // type agnostic
            foreach(int tail in child.Tail)
            {
                bw.Write(tail);
                len += 4;
            }

            return len;
        }

        ///////////////////////////////////////////////////////////////////////

        public bool WriteLoc(string file_name)
        {
            bool valid = true;

            valid &= (rootChild != null);

            if (valid)
            {
                BinaryWriter bw = new BinaryWriter(File.Create(file_name));

                int root_cnt = rootChild.Children.Count;
                bw.Write((byte)(root_cnt & 0xFF));

                int hdr_offset = (int)bw.BaseStream.Length;

                for (int i = 0; i < root_cnt - 1; ++i)
                {
                    bw.Write((int)0xF0F0F0F); // PLACEHOLDER SIZE VALUES
                }

                var child_lengths = new List<int>();
                foreach (CType generic_child in rootChild.Children)
                {
                    int child_length = WriteChild(bw, generic_child);
                    child_lengths.Add(child_length);
                }

                if (root_cnt > 1)
                {
                    // revist all data length
                    bw.Seek(hdr_offset, SeekOrigin.Begin);

                    int sum = 0;
                    for (int i = 0; i < root_cnt - 1; ++i)
                    {
                        sum += child_lengths[i];
                        bw.Write(sum);
                    }
                }

                bw.Close();
            }

            return valid;
        }

        ///////////////////////////////////////////////////////////////////////
    }
}
