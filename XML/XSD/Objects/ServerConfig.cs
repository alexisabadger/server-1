// ------------------------------------------------------------------------------
//  <auto-generated>
//    Generated by Xsd2Code++. Version 6.0.20.0. www.xsd2code.com
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

[System.CodeDom.Compiler.GeneratedCodeAttribute("System.Xml", "4.8.4161.0")]
[Serializable]
[DebuggerStepThrough]
[DesignerCategoryAttribute("code")]
[XmlTypeAttribute(Namespace="http://www.hybrasyl.com/XML/Hybrasyl/2020-02")]
[XmlRootAttribute(Namespace="http://www.hybrasyl.com/XML/Hybrasyl/2020-02", IsNullable=false)]
public partial class ServerConfig
{
    #region Private fields
    private LogConfig _logging;
    private DataStore _dataStore;
    private Network _network;
    private ApiEndpoints _apiEndpoints;
    private Access _access;
    private List<GlobalBoard> _boards;
    private Time _time;
    private Handlers _handlers;
    private string _motd;
    private ServerPlugins _plugins;
    private List<ClientSetting> _clientSettings;
    private string _worldDataDir;
    private static XmlSerializer _serializerXml;
    #endregion
    
    public ServerConfig()
    {
        _apiEndpoints = new ApiEndpoints();
        _network = new Network();
        _dataStore = new DataStore();
    }
    
    public LogConfig Logging
    {
        get
        {
            return _logging;
        }
        set
        {
            _logging = value;
        }
    }
    
    public DataStore DataStore
    {
        get
        {
            return _dataStore;
        }
        set
        {
            _dataStore = value;
        }
    }
    
    public Network Network
    {
        get
        {
            return _network;
        }
        set
        {
            _network = value;
        }
    }
    
    public ApiEndpoints ApiEndpoints
    {
        get
        {
            return _apiEndpoints;
        }
        set
        {
            _apiEndpoints = value;
        }
    }
    
    public Access Access
    {
        get
        {
            return _access;
        }
        set
        {
            _access = value;
        }
    }
    
    [XmlArrayItemAttribute("Board", IsNullable=false)]
    public List<GlobalBoard> Boards
    {
        get
        {
            return _boards;
        }
        set
        {
            _boards = value;
        }
    }
    
    public Time Time
    {
        get
        {
            return _time;
        }
        set
        {
            _time = value;
        }
    }
    
    public Handlers Handlers
    {
        get
        {
            return _handlers;
        }
        set
        {
            _handlers = value;
        }
    }
    
    [StringLengthAttribute(65534, MinimumLength=1)]
    public string Motd
    {
        get
        {
            return _motd;
        }
        set
        {
            _motd = value;
        }
    }
    
    public ServerPlugins Plugins
    {
        get
        {
            return _plugins;
        }
        set
        {
            _plugins = value;
        }
    }
    
    [XmlArrayItemAttribute("Setting", IsNullable=false)]
    public List<ClientSetting> ClientSettings
    {
        get
        {
            return _clientSettings;
        }
        set
        {
            _clientSettings = value;
        }
    }
    
    public string WorldDataDir
    {
        get
        {
            return _worldDataDir;
        }
        set
        {
            _worldDataDir = value;
        }
    }
    
    private static XmlSerializer SerializerXml
    {
        get
        {
            if ((_serializerXml == null))
            {
                _serializerXml = new XmlSerializerFactory().CreateSerializer(typeof(ServerConfig));
            }
            return _serializerXml;
        }
    }
    
    #region Serialize/Deserialize
    /// <summary>
    /// Serialize ServerConfig object
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
            SerializerXml.Serialize(xmlWriter, this);
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
    /// Deserializes ServerConfig object
    /// </summary>
    /// <param name="input">string to deserialize</param>
    /// <param name="obj">Output ServerConfig object</param>
    /// <param name="exception">output Exception value if deserialize failed</param>
    /// <returns>true if this Serializer can deserialize the object; otherwise, false</returns>
    public static bool Deserialize(string input, out ServerConfig obj, out Exception exception)
    {
        exception = null;
        obj = default(ServerConfig);
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
    
    public static bool Deserialize(string input, out ServerConfig obj)
    {
        Exception exception = null;
        return Deserialize(input, out obj, out exception);
    }
    
    public static ServerConfig Deserialize(string input)
    {
        StringReader stringReader = null;
        try
        {
            stringReader = new StringReader(input);
            return ((ServerConfig)(SerializerXml.Deserialize(XmlReader.Create(stringReader))));
        }
        finally
        {
            if ((stringReader != null))
            {
                stringReader.Dispose();
            }
        }
    }
    
    public static ServerConfig Deserialize(Stream s)
    {
        return ((ServerConfig)(SerializerXml.Deserialize(s)));
    }
    #endregion
    
    /// <summary>
    /// Serializes current ServerConfig object into file
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
    /// Deserializes xml markup from file into an ServerConfig object
    /// </summary>
    /// <param name="fileName">File to load and deserialize</param>
    /// <param name="obj">Output ServerConfig object</param>
    /// <param name="exception">output Exception value if deserialize failed</param>
    /// <returns>true if this Serializer can deserialize the object; otherwise, false</returns>
    public static bool LoadFromFile(string fileName, out ServerConfig obj, out Exception exception)
    {
        exception = null;
        obj = default(ServerConfig);
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
    
    public static bool LoadFromFile(string fileName, out ServerConfig obj)
    {
        Exception exception = null;
        return LoadFromFile(fileName, out obj, out exception);
    }
    
    public static ServerConfig LoadFromFile(string fileName)
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
