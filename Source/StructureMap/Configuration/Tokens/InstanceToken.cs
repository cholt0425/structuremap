using System;
using System.Collections;
using StructureMap.Configuration.Tokens.Properties;
using StructureMap.Exceptions;
using StructureMap.Graph;

namespace StructureMap.Configuration.Tokens
{
    [Serializable]
    public class InstanceToken : GraphObject
    {
        private string _instanceKey;
        private string _concreteKey;
        private DefinitionSource _source = DefinitionSource.Explicit;
        private Hashtable _properties;
        [NonSerialized] private InstanceMemento _memento;
        private string _pluginTypeName;
        private string _mementoString;
        private string _templateKey;
        private Type _pluginType;

        public InstanceToken() : base()
        {
            _properties = new Hashtable();
        }

        public InstanceToken(Type pluginType, PluginGraphReport report, InstanceMemento memento) : this()
        {
            _memento = memento;
            _mementoString = memento.ToString();
            _pluginTypeName = TypePath.GetAssemblyQualifiedName(pluginType);
            _pluginType = pluginType;

            InstanceKey = memento.InstanceKey;
            ConcreteKey = memento.ConcreteKey;
            try
            {
                PluginToken plugin = report.FindPlugin(pluginType, ConcreteKey);
                if (plugin == null)
                {
                    if (memento.TemplateName == string.Empty)
                    {
                        logInvalidPlugin(_pluginTypeName);
                    }
                    else
                    {
                        buildPropertyFromTemplatedMemento(report, _pluginType, memento);
                    }
                }
                else
                {
                    buildPropertiesFromPlugin(plugin, memento, report);
                }
            }
            catch (MissingPluginFamilyException ex)
            {
                Problem problem = new Problem(ConfigurationConstants.INVALID_PLUGIN_FAMILY, ex);
                LogProblem(problem);
            }
        }

        private void buildPropertyFromTemplatedMemento(PluginGraphReport report, Type pluginType,
                                                       InstanceMemento memento)
        {
            _templateKey = memento.TemplateName;
            TemplateToken token = report.FindTemplate(pluginType, _templateKey);
            IProperty[] properties = TemplateProperty.GetTemplateProperties(memento, token);
            foreach (IProperty property in properties)
            {
                AddProperty(property);
            }
        }

        private void logInvalidPlugin(string pluginTypeName)
        {
            string message = string.Format("Looking for Plugin '{0}' for PluginFamily {1}", ConcreteKey, pluginTypeName);
            Problem problem = new Problem(ConfigurationConstants.INVALID_PLUGIN, message);
            LogProblem(problem);
        }

        private void buildPropertiesFromPlugin(PluginToken plugin, InstanceMemento memento, PluginGraphReport report)
        {
            foreach (PropertyDefinition definition in plugin.Properties)
            {
                IProperty property = definition.CreateProperty(memento, report);
                AddProperty(property);
            }
        }

        public string InstanceKey
        {
            get { return _instanceKey; }
            set { _instanceKey = value; }
        }

        public string ConcreteKey
        {
            get { return _concreteKey; }
            set { _concreteKey = value; }
        }

        public DefinitionSource Source
        {
            get { return _source; }
            set { _source = value; }
        }

        public IProperty[] Properties
        {
            get
            {
                ArrayList list = new ArrayList(_properties.Values);
                return (IProperty[]) list.ToArray(typeof (IProperty));
            }
            set
            {
                _properties = new Hashtable();
                foreach (Property property in value)
                {
                    AddProperty(property);
                }
            }
        }

        public IProperty this[string propertyName]
        {
            get { return (IProperty) _properties[propertyName]; }
        }

        public void AddProperty(IProperty property)
        {
            _properties.Add(property.PropertyName, property);
        }

        public void Validate(IInstanceValidator validator)
        {
            try
            {
                object target = validator.CreateObject(_pluginType, _memento);
                validateInstance(target);
            }
            catch (Exception ex)
            {
                Problem problem = new Problem(ConfigurationConstants.COULD_NOT_CREATE_INSTANCE, ex);
                LogProblem(problem);
            }

            foreach (IProperty property in _properties.Values)
            {
                property.Validate(validator);
            }
        }

        private void validateInstance(object target)
        {
            try
            {
                ValidationMethodAttribute.CallValidationMethods(target);
            }
            catch (Exception ex)
            {
                if (ex.InnerException != null)
                {
                    ex = ex.InnerException;
                }

                Problem problem = new Problem(ConfigurationConstants.VALIDATION_METHOD_FAILURE, ex);
                LogProblem(problem);
            }
        }

        public string PluginTypeName
        {
            get { return _pluginTypeName; }
        }

        public override GraphObject[] Children
        {
            get
            {
                GraphObject[] returnValue = new GraphObject[_properties.Count];
                _properties.Values.CopyTo(returnValue, 0);

                return returnValue;
            }
        }

        public override void AcceptVisitor(IConfigurationVisitor visitor)
        {
            visitor.HandleInstance(this);
        }

        protected override string key
        {
            get { return InstanceKey; }
        }

        public string TemplateKey
        {
            get { return _templateKey; }
        }

        public override string ToString()
        {
            if (TemplateKey != string.Empty && TemplateKey != null)
            {
                return
                    string.Format("{0} Instance {1} of Type {2} (Template {3})", Source.ToString(), InstanceKey,
                                  ConcreteKey, TemplateKey);
            }
            return string.Format("{0} Instance '{1}' of Type {2}", Source.ToString(), InstanceKey, ConcreteKey);
        }
    }
}