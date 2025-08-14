using System;
using System.Management.Automation;
using System.Management.Automation.Language;
using System.Management.Automation.Runspaces;

using System.Net;
using System;
using System.Runtime.CompilerServices;
using System.Text;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Management.Automation.Internal;



[assembly: IgnoresAccessChecksTo("System.Management.Automation")]
namespace PSHack
{


    // Token: 0x0200024E RID: 590

    internal class CompiledScriptBlockData
    {
        private string _scriptText;

        private IParameterMetadataProvider _ast;

        private RuntimeDefinedParameterDictionary _runtimeDefinedParameterDictionary;

        private Attribute[] _attributes;

        private bool _usesCmdletBinding;

        private bool _compiledOptimized;

        private bool _compiledUnoptimized;

        private bool _hasSuspicousContent;

        private MergedCommandParameterMetadata _parameterMetadata;

        internal IParameterMetadataProvider Ast => _ast ?? DelayParseScriptText();

        internal Type LocalsMutableTupleType { get; set; }

        internal Type UnoptimizedLocalsMutableTupleType { get; set; }

        internal Func<MutableTuple> LocalsMutableTupleCreator { get; set; }

        internal Func<MutableTuple> UnoptimizedLocalsMutableTupleCreator { get; set; }

        internal Dictionary<string, int> NameToIndexMap { get; set; }

        internal Action<FunctionContext> DynamicParamBlock { get; set; }

        internal Action<FunctionContext> UnoptimizedDynamicParamBlock { get; set; }

        internal Action<FunctionContext> BeginBlock { get; set; }

        internal Action<FunctionContext> UnoptimizedBeginBlock { get; set; }

        internal Action<FunctionContext> ProcessBlock { get; set; }

        internal Action<FunctionContext> UnoptimizedProcessBlock { get; set; }

        internal Action<FunctionContext> EndBlock { get; set; }

        internal Action<FunctionContext> UnoptimizedEndBlock { get; set; }

        internal IScriptExtent[] SequencePoints { get; set; }

        internal bool DebuggerHidden { get; set; }

        internal bool DebuggerStepThrough { get; set; }

        internal Guid Id { get; private set; }

        internal bool HasLogged { get; set; }

        internal bool IsFilter { get; private set; }

        internal bool IsProductCode { get; private set; }

        internal bool HasSuspiciousContent
        {
            get
            {
                return _hasSuspicousContent;
            }
            set
            {
                _hasSuspicousContent = value;
            }
        }

        internal bool UsesCmdletBinding
        {
            get
            {
                if (_attributes != null)
                {
                    return _usesCmdletBinding;
                }
                return Ast.UsesCmdletBinding();
            }
        }

        internal RuntimeDefinedParameterDictionary RuntimeDefinedParameters
        {
            get
            {
                if (_runtimeDefinedParameterDictionary == null)
                {
                    InitializeMetadata();
                }
                return _runtimeDefinedParameterDictionary;
            }
        }

        internal CmdletBindingAttribute CmdletBindingAttribute
        {
            get
            {
                if (_runtimeDefinedParameterDictionary == null)
                {
                    InitializeMetadata();
                }
                if (!_usesCmdletBinding)
                {
                    return null;
                }
                return (CmdletBindingAttribute)_attributes.FirstOrDefault((Attribute attr) => attr is CmdletBindingAttribute);
            }
        }

        internal ObsoleteAttribute ObsoleteAttribute
        {
            get
            {
                if (_runtimeDefinedParameterDictionary == null)
                {
                    InitializeMetadata();
                }
                return (ObsoleteAttribute)_attributes.FirstOrDefault((Attribute attr) => attr is ObsoleteAttribute);
            }
        }

        internal CompiledScriptBlockData(IParameterMetadataProvider ast, bool isFilter)
        {
            _ast = ast;
            IsFilter = isFilter;
            Id = Guid.NewGuid();
        }

        internal CompiledScriptBlockData(string scriptText, bool isProductCode)
        {
            IsProductCode = isProductCode;
            _scriptText = scriptText;
            Id = Guid.NewGuid();
        }

        internal bool Compile(bool optimized)
        {
            if (_attributes == null)
            {
                InitializeMetadata();
            }
            if (optimized && NameToIndexMap == null)
            {
                CompileOptimized();
            }
            optimized = optimized && !VariableAnalysis.AnyVariablesCouldBeAllScope(NameToIndexMap);
            if (!optimized && !_compiledUnoptimized)
            {
                CompileUnoptimized();
            }
            else if (optimized && !_compiledOptimized)
            {
                CompileOptimized();
            }
            return optimized;
        }

        private void InitializeMetadata()
        {
            lock (this)
            {
                if (_attributes != null)
                {
                    return;
                }
                CmdletBindingAttribute cmdletBindingAttribute = null;
                Attribute[] array;
                if (!Ast.HasAnyScriptBlockAttributes())
                {
                    array = Utils.EmptyArray<Attribute>();
                }
                else
                {
                    array = Ast.GetScriptBlockAttributes().ToArray();
                    Attribute[] array2 = array;
                    foreach (Attribute attribute in array2)
                    {
                        if (attribute is CmdletBindingAttribute)
                        {
                            cmdletBindingAttribute = cmdletBindingAttribute ?? ((CmdletBindingAttribute)attribute);
                        }
                        else if (attribute is DebuggerHiddenAttribute)
                        {
                            DebuggerHidden = true;
                        }
                        else if (attribute is DebuggerStepThroughAttribute || attribute is DebuggerNonUserCodeAttribute)
                        {
                            DebuggerStepThrough = true;
                        }
                    }
                    _usesCmdletBinding = cmdletBindingAttribute != null;
                }
                bool automaticPositions = cmdletBindingAttribute?.PositionalBinding ?? true;
                RuntimeDefinedParameterDictionary parameterMetadata = Ast.GetParameterMetadata(automaticPositions, ref _usesCmdletBinding);
                _attributes = array;
                _runtimeDefinedParameterDictionary = parameterMetadata;
            }
        }

        private void CompileUnoptimized()
        {
            lock (this)
            {
                if (!_compiledUnoptimized)
                {
                    ReallyCompile(optimize: false);
                    _compiledUnoptimized = true;
                }
            }
        }

        private void CompileOptimized()
        {
            lock (this)
            {
                if (!_compiledOptimized)
                {
                    ReallyCompile(optimize: true);
                    _compiledOptimized = true;
                }
            }
        }


        static void CopyPropertiesAndFields(object source, object target)
        {
            var sourceType = source.GetType();
            var targetType = target.GetType();

            foreach (var sourceField in sourceType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                var targetField = targetType.GetField(sourceField.Name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (targetField != null)
                {
                    var value = sourceField.GetValue(source);
                    targetField.SetValue(target, value);
                }
            }

            foreach (var sourceProperty in sourceType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                var targetProperty = targetType.GetProperty(sourceProperty.Name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (targetProperty != null && targetProperty.CanWrite)
                {
                    var value = sourceProperty.GetValue(source);
                    targetProperty.SetValue(target, value);
                }
            }
        }

        private void ReallyCompile(bool optimize)
        {
            if (!IsProductCode && SecuritySupport.IsProductBinary(((Ast)_ast).Extent.File))
            {
                IsProductCode = true;
            }
            bool num = ParserEventSource.Log.IsEnabled();
            if (num)
            {
                IScriptExtent extent = _ast.Body.Extent;
                string text = extent.Text;
                ParserEventSource.Log.CompileStart(ParserEventSource.GetFileOrScript(extent.File, text), text.Length, optimize);
            }
            PerformSecurityChecks();

            //  new Compiler().Compile(this, optimize);
            System.Management.Automation.CompiledScriptBlockData compiledScriptBlockData = new System.Management.Automation.CompiledScriptBlockData("", false);
            CopyPropertiesAndFields(this, compiledScriptBlockData);
            new Compiler().Compile(compiledScriptBlockData, optimize);
            CopyPropertiesAndFields(compiledScriptBlockData, this);
            if (num)
            {
                ParserEventSource.Log.CompileStop();
            }
        }

        private void PerformSecurityChecks()
        {
            if (Ast is ScriptBlockAst scriptBlockAst)
            {
                IScriptExtent extent = scriptBlockAst.Extent;
                AmsiUtils.AmsiNativeMethods.AMSI_RESULT aMSI_RESULT = AmsiUtils.ScanContent(extent.Text, extent.File);
                if (aMSI_RESULT == AmsiUtils.AmsiNativeMethods.AMSI_RESULT.AMSI_RESULT_DETECTED)
                {
                    ParseError parseError = new ParseError(extent, "ScriptContainedMaliciousContent", ParserStrings.ScriptContainedMaliciousContent);
                    throw new ParseException(new ParseError[1] { parseError });
                }
                if (aMSI_RESULT >= AmsiUtils.AmsiNativeMethods.AMSI_RESULT.AMSI_RESULT_BLOCKED_BY_ADMIN_BEGIN && aMSI_RESULT <= AmsiUtils.AmsiNativeMethods.AMSI_RESULT.AMSI_RESULT_BLOCKED_BY_ADMIN_END)
                {
                    ParseError parseError2 = new ParseError(extent, "ScriptHasAdminBlockedContent", StringUtil.Format(ParserStrings.ScriptHasAdminBlockedContent, aMSI_RESULT));
                    throw new ParseException(new ParseError[1] { parseError2 });
                }
                if (ScriptBlock.CheckSuspiciousContent(scriptBlockAst) != null)
                {
                    HasSuspiciousContent = true;
                }
            }
        }

        private IParameterMetadataProvider DelayParseScriptText()
        {
            lock (this)
            {
                if (_ast != null)
                {
                    return _ast;
                }
                _ast = new Parser().Parse(null, _scriptText, null, out var errors, ParseMode.Default);
                if (errors.Length != 0)
                {
                    throw new ParseException(errors);
                }
                _scriptText = null;
                return _ast;
            }
        }

        internal bool GetIsConfiguration()
        {
            if (_ast is ScriptBlockAst scriptBlockAst)
            {
                return scriptBlockAst.IsConfiguration;
            }
            return false;
        }

        internal List<Attribute> GetAttributes()
        {
            if (_attributes == null)
            {
                InitializeMetadata();
            }
            return _attributes.ToList();
        }

        public MergedCommandParameterMetadata GetParameterMetadata(ScriptBlock scriptBlock)
        {
            if (_parameterMetadata == null)
            {
                lock (this)
                {
                    if (_parameterMetadata == null)
                    {
                        CommandMetadata commandMetadata = new CommandMetadata(scriptBlock, "", LocalPipeline.GetExecutionContextFromTLS());
                        _parameterMetadata = commandMetadata.StaticCommandParameterMetadata;
                    }
                }
            }
            return _parameterMetadata;
        }

        public override string ToString()
        {
            if (_scriptText != null)
            {
                return _scriptText;
            }
            if (_ast is ScriptBlockAst scriptBlockAst)
            {
                return scriptBlockAst.ToStringForSerialization();
            }
            if (_ast is CompilerGeneratedMemberFunctionAst compilerGeneratedMemberFunctionAst)
            {
                return compilerGeneratedMemberFunctionAst.Extent.Text;
            }
            FunctionDefinitionAst functionDefinitionAst = (FunctionDefinitionAst)_ast;
            if (functionDefinitionAst.Parameters == null)
            {
                return functionDefinitionAst.Body.ToStringForSerialization();
            }
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append(functionDefinitionAst.GetParamTextFromParameterList());
            stringBuilder.Append(functionDefinitionAst.Body.ToStringForSerialization());
            return stringBuilder.ToString();
        }
    }

    public class Program
    {
        public static void Main(string[] args)
        {

            var obfcode = @"$obfuscated = [System.Text.Encoding]::UTF8.GetString([System.Convert]::FromBase64String('V3JpdGUtSG9zdCAiSGVsbG8sIFdvcmxkISI=')); Invoke-Expression $obfuscated; echo $obfuscated";
            var compiler = new Compiler();
            var scriptblock = new CompiledScriptBlockData(obfcode, false);
            
            scriptblock.Compile(false);
            Console.WriteLine(scriptblock.Ast);


            Runspace runspace = RunspaceFactory.CreateRunspace();
            runspace.Open();
            Runspace.DefaultRunspace = runspace;
            PowerShell powerShell = PowerShell.Create();

            ScriptBlock scriptBlock = ScriptBlock.Create(obfcode);
            
            var results = scriptBlock.Invoke("C#");
            foreach (var result in results)
                Console.WriteLine(result);
            }   
    }
}

namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public class IgnoresAccessChecksToAttribute : Attribute
    {
        public IgnoresAccessChecksToAttribute(string assemblyName)
        {
            AssemblyName = assemblyName;
        }

        public string AssemblyName { get; }
    }
}