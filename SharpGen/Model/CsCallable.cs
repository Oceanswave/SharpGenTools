﻿using SharpGen.Config;
using SharpGen.CppModel;
using SharpGen.Transform;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;

namespace SharpGen.Model
{
    [DataContract]
    public abstract class CsCallable : CsBase
    {
        public override CppElement CppElement
        {
            get => base.CppElement;
            set
            {
                base.CppElement = value;
                CppSignature = CppElement.ToString();
                ShortName = CppElement.ToShortString();
                CallingConvention = GetCallingConvention((CppCallable)value);
            }
        }

        protected abstract int MaxSizeReturnParameter { get; }

        protected CsCallable()
        {

        }

        protected CsCallable(CppCallable callable)
        {
            CppElement = callable;
        }

        private List<CsParameter> _parameters;
        public List<CsParameter> Parameters
        {
            get { return _parameters ?? (_parameters = Items.OfType<CsParameter>().ToList()); }
        }

        public IEnumerable<CsParameter> PublicParameters
        {
            get
            {
                return Items.OfType<CsParameter>().Where(param => !param.IsUsedAsReturnType);
            }
        }

        [DataMember]
        public string CallingConvention { get; set; }

        private static string GetCallingConvention(CppCallable method)
        {
            switch (method.CallingConvention)
            {
                case CppCallingConvention.StdCall:
                    return "StdCall";
                case CppCallingConvention.CDecl:
                    return "Cdecl";
                case CppCallingConvention.ThisCall:
                    return "ThisCall";
                default:
                    return "Winapi";
            }
        }

        public override void FillDocItems(IList<string> docItems, IDocumentationLinker manager)
        {
            foreach (var param in PublicParameters)
                docItems.Add("<param name=\"" + param.Name + "\">" + manager.GetSingleDoc(param) + "</param>");

            if (HasReturnType)
                docItems.Add("<returns>" + GetReturnTypeDoc(manager) + "</returns>");
        }

        public bool IsReturnStructLarge
        {
            get
            {
                if ((ReturnValue.MarshalType ?? ReturnValue.PublicType) is CsStruct csStruct)
                {
                    if (ReturnValue.MarshalType is CsFundamentalType fundamental && fundamental.Type == typeof(IntPtr))
                        return false;

                    return csStruct.Size > MaxSizeReturnParameter;
                }
                return false;
            }
        }

        protected override void UpdateFromMappingRule(MappingRule tag)
        {
            base.UpdateFromMappingRule(tag);

            if (tag.MethodCheckReturnType.HasValue)
                CheckReturnType = tag.MethodCheckReturnType.Value;

            if (tag.ParameterUsedAsReturnType.HasValue)
                ForceReturnType = tag.ParameterUsedAsReturnType.Value;

            if (tag.AlwaysReturnHResult.HasValue)
                AlwaysReturnHResult = tag.AlwaysReturnHResult.Value;

            if (tag.RawPtr.HasValue)
                RequestRawPtr = tag.RawPtr.Value;
        }

        [DataMember]
        public bool RequestRawPtr { get; set; }

        [DataMember]
        public InteropMethodSignature Interop { get; set; }

        private string _cppSignature;

        [DataMember]
        public string CppSignature
        {
            get
            {
                return _cppSignature ?? "Unknown";
            }
            set => _cppSignature = value;
        }

        public override string DocUnmanagedName
        {
            get { return CppSignature; }
        }

        [DataMember]
        public string ShortName { get; set; }

        public override string DocUnmanagedShortName
        {
            get => ShortName;
        }

        [DataMember]
        public bool CheckReturnType { get; set; }

        [DataMember]
        public bool ForceReturnType { get; set; }

        [DataMember]
        public bool HideReturnType { get; set; }

        [DataMember]
        public bool AlwaysReturnHResult { get; set; }

        public bool HasReturnType
        {
            get { return !(ReturnValue.PublicType is CsFundamentalType fundamental && fundamental.Type == typeof(void)); }
        }

        public bool HasPublicReturnType
        {
            get
            {
                foreach (var param in Parameters)
                {
                    if (param.IsUsedAsReturnType)
                        return true;
                }

                return HasReturnType;
            }
        }

        [DataMember]
        public CsReturnValue ReturnValue { get; set; }


        /// <summary>
        /// Return the Public return type. If a out parameter is used as a public return type
        /// then use the type of the out parameter for the public API.
        /// </summary>
        public bool HasReturnTypeParameter
        {
            get
            {
                return Parameters.Any(param => param.IsUsedAsReturnType);
            }
        }

        /// <summary>
        /// Return the Public return type. If a out parameter is used as a public return type
        /// then use the type of the out parameter for the public API.
        /// </summary>
        public string PublicReturnTypeQualifiedName
        {
            get
            {
                foreach (var param in Parameters)
                {
                    if (param.IsUsedAsReturnType)
                        return param.PublicType.QualifiedName;
                }

                if (HideReturnType && !ForceReturnType)
                    return "void";

                return ReturnValue.PublicType.QualifiedName;
            }
        }

        /// <summary>
        /// Returns the documentation for the return type
        /// </summary>
        public string GetReturnTypeDoc(IDocumentationLinker linker)
        {
            foreach (var param in Parameters)
            {
                if (param.IsUsedAsReturnType)
                {
                    return linker.GetSingleDoc(param);
                }
            }
            return linker.GetSingleDoc(ReturnValue);
        }

        /// <summary>
        /// Return the name of the variable used to return the value
        /// </summary>
        public string ReturnName
        {
            get
            {
                foreach (var param in Parameters)
                {
                    if (param.IsUsedAsReturnType)
                        return param.Name;
                }
                return "__result__";
            }
        }

        public override object Clone()
        {
            var method = (CsCallable)base.Clone();

            // Clear cached parameters
            method._parameters = null;
            method.ResetItems();
            foreach (var parameter in Parameters)
                method.Add((CsParameter)parameter.Clone());
            method.Parent = null;
            return method;
        }
    }
}
