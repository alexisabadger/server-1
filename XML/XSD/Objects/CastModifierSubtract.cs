// ------------------------------------------------------------------------------
//  <auto-generated>
//    Generated by Xsd2Code++. Version 5.2.0.0. www.xsd2code.com
//  </auto-generated>
// ------------------------------------------------------------------------------
#pragma warning disable
namespace Hybrasyl.Xml
{
using System;
using System.Diagnostics;
using System.Xml.Serialization;
using System.Runtime.Serialization;
using System.Collections;
using System.Xml.Schema;
using System.ComponentModel;
using System.Xml;
using System.IO;
using System.Text;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

[System.CodeDom.Compiler.GeneratedCodeAttribute("System.Xml", "4.8")]
[Serializable]
[DebuggerStepThrough]
[System.ComponentModel.DesignerCategoryAttribute("code")]
[XmlTypeAttribute(Namespace="http://www.hybrasyl.com/XML/Hybrasyl/2020-02")]
public partial class CastModifierSubtract
{
    #region Private fields
    private int _match;
    private int _amount;
    private int _min;
    private int _max;
    private static XmlSerializer _serializer;
    #endregion
    
    public CastModifierSubtract()
    {
        _match = -1;
        _min = -1;
        _max = 255;
    }
    
    [XmlAttribute]
    [DefaultValue(-1)]
    public int Match
    {
        get
        {
            return _match;
        }
        set
        {
            _match = value;
        }
    }
    
    [XmlAttribute]
    public int Amount
    {
        get
        {
            return _amount;
        }
        set
        {
            _amount = value;
        }
    }
    
    [XmlAttribute]
    [DefaultValue(-1)]
    public int Min
    {
        get
        {
            return _min;
        }
        set
        {
            _min = value;
        }
    }
    
    [XmlAttribute]
    [DefaultValue(255)]
    public int Max
    {
        get
        {
            return _max;
        }
        set
        {
            _max = value;
        }
    }
    
    private static XmlSerializer SerializerXML
    {
        get
        {
            if ((_serializer == null))
            {
                _serializer = new XmlSerializerFactory().CreateSerializer(typeof(CastModifierSubtract));
            }
            return _serializer;
        }
    }
    
    #region Serialize/Deserialize
    /// <summary>
    /// Serialize CastModifierSubtract object
    /// </summary>
    /// <returns>XML value</returns>
    public virtual string Serialize()
    {
        StreamReader streamReader = null;
        MemoryStream memoryStream = null;
        try
        {
            memoryStream = new MemoryStream();
            System.Xml.XmlWriterSettings xmlWriterSettings = new System.Xml.XmlWriterSettings();
            xmlWriterSettings.Indent = true;
            xmlWriterSettings.IndentChars = "  ";
            System.Xml.XmlWriter xmlWriter = XmlWriter.Create(memoryStream, xmlWriterSettings);
            SerializerXML.Serialize(xmlWriter, this);
            memoryStream.Seek(0, SeekOrigin.Begin);
            streamReader = new StreamReader(memoryStream);
            return streamReader.ReadToEnd();
        }
        finally
        {
            if ((streamReader != null))
            {
                streamReader.Dispose();
            }
            if ((memoryStream != null))
            {
                memoryStream.Dispose();
            }
        }
    }
    
    /// <summary>
    /// Deserializes CastModifierSubtract object
    /// </summary>
    /// <param name="input">string workflow markup to deserialize</param>
    /// <param name="obj">Output CastModifierSubtract object</param>
    /// <param name="exception">output Exception value if deserialize failed</param>
    /// <returns>true if this Serializer can deserialize the object; otherwise, false</returns>
    public static bool Deserialize(string input, out CastModifierSubtract obj, out Exception exception)
    {
        exception = null;
        obj = default(CastModifierSubtract);
        try
        {
            obj = Deserialize(input);
            return true;
        }
        catch (Exception ex)
        {
            exception = ex;
            return false;
        }
    }
    
    public static bool Deserialize(string input, out CastModifierSubtract obj)
    {
        Exception exception = null;
        return Deserialize(input, out obj, out exception);
    }
    
    public static CastModifierSubtract Deserialize(string input)
    {
        StringReader stringReader = null;
        try
        {
            stringReader = new StringReader(input);
            return ((CastModifierSubtract)(SerializerXML.Deserialize(XmlReader.Create(stringReader))));
        }
        finally
        {
            if ((stringReader != null))
            {
                stringReader.Dispose();
            }
        }
    }
    
    public static CastModifierSubtract Deserialize(Stream s)
    {
        return ((CastModifierSubtract)(SerializerXML.Deserialize(s)));
    }
    #endregion
    
    /// <summary>
    /// Serializes current CastModifierSubtract object into file
    /// </summary>
    /// <param name="fileName">full path of outupt xml file</param>
    /// <param name="exception">output Exception value if failed</param>
    /// <returns>true if can serialize and save into file; otherwise, false</returns>
    public virtual bool SaveToFile(string fileName, out Exception exception)
    {
        exception = null;
        try
        {
            SaveToFile(fileName);
            return true;
        }
        catch (Exception e)
        {
            exception = e;
            return false;
        }
    }
    
    public virtual void SaveToFile(string fileName)
    {
        StreamWriter streamWriter = null;
        try
        {
            string dataString = Serialize();
            FileInfo outputFile = new FileInfo(fileName);
            streamWriter = outputFile.CreateText();
            streamWriter.WriteLine(dataString);
            streamWriter.Close();
        }
        finally
        {
            if ((streamWriter != null))
            {
                streamWriter.Dispose();
            }
        }
    }
    
    /// <summary>
    /// Deserializes xml markup from file into an CastModifierSubtract object
    /// </summary>
    /// <param name="fileName">string xml file to load and deserialize</param>
    /// <param name="obj">Output CastModifierSubtract object</param>
    /// <param name="exception">output Exception value if deserialize failed</param>
    /// <returns>true if this Serializer can deserialize the object; otherwise, false</returns>
    public static bool LoadFromFile(string fileName, out CastModifierSubtract obj, out Exception exception)
    {
        exception = null;
        obj = default(CastModifierSubtract);
        try
        {
            obj = LoadFromFile(fileName);
            return true;
        }
        catch (Exception ex)
        {
            exception = ex;
            return false;
        }
    }
    
    public static bool LoadFromFile(string fileName, out CastModifierSubtract obj)
    {
        Exception exception = null;
        return LoadFromFile(fileName, out obj, out exception);
    }
    
    public static CastModifierSubtract LoadFromFile(string fileName)
    {
        FileStream file = null;
        StreamReader sr = null;
        try
        {
            file = new FileStream(fileName, FileMode.Open, FileAccess.Read);
            sr = new StreamReader(file);
            string dataString = sr.ReadToEnd();
            sr.Close();
            file.Close();
            return Deserialize(dataString);
        }
        finally
        {
            if ((file != null))
            {
                file.Dispose();
            }
            if ((sr != null))
            {
                sr.Dispose();
            }
        }
    }
}
}
#pragma warning restore
