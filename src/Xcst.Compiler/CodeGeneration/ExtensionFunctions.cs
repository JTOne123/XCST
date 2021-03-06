﻿// Copyright 2015 Max Toro Q.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;
using Saxon.Api;
using Xcst.Runtime;
using XPathException = net.sf.saxon.trans.XPathException;

namespace Xcst.Compiler.CodeGeneration {

   using static XcstCompiler;

   class ExtensionFunctions {

      internal static string
      LocalPath(Uri uri) {

         if (uri is null) throw new ArgumentNullException(nameof(uri));

         if (!uri.IsAbsoluteUri) {
            return uri.OriginalString;
         }

         if (uri.IsFile) {
            return uri.LocalPath;
         }

         return uri.AbsoluteUri;
      }

      internal static Uri?
      FindNamedPackage(string packageName, string packagesLocation, string fileExtension) {

         if (packageName is null) throw new ArgumentNullException(nameof(packageName));
         if (packageName.Length == 0) throw new ArgumentException(nameof(packageName));

         string dir = packagesLocation;
         string search = "*." + fileExtension;

         if (!Directory.Exists(dir)) {
            return null;
         }

         foreach (string path in Directory.EnumerateFiles(dir, search, SearchOption.AllDirectories)) {

            if (Path.GetFileNameWithoutExtension(path)[0] == '_') {
               continue;
            }

            var readerSettings = new XmlReaderSettings {
               IgnoreComments = true,
               IgnoreProcessingInstructions = true,
               IgnoreWhitespace = true,
               ValidationType = ValidationType.None,
               DtdProcessing = DtdProcessing.Ignore
            };

            using (var reader = XmlReader.Create(path, readerSettings)) {

               while (reader.Read()) {

                  if (reader.NodeType == XmlNodeType.Element) {

                     if (reader.LocalName == "package"
                        && reader.NamespaceURI == XmlNamespaces.Xcst
                        && SimpleContent.Trim(reader.GetAttribute("name")) == packageName) {

                        return new Uri(path, UriKind.Absolute);
                     }

                     break;
                  }
               }
            }
         }

         return null;
      }

      internal static Type?
      ResolvePackageType(string packageName) {

         Type? type = null;

         foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies()) {

            Type? type2 = asm.GetType(packageName);

            if (type2 != null) {

               if (type != null && type2 != type) {
                  throw new Exception($"Ambiguous type '{packageName}'.");
               }

               type = type2;
            }
         }

         return type;
      }

      internal static int
      StringId(string str) => str.GetHashCode();
   }

   class LineNumberFunction : ExtensionFunctionDefinition {

      public override QName
      FunctionName { get; } = CompilerQName("_line-number");

      public override XdmSequenceType[]
      ArgumentTypes { get; } = {
         new XdmSequenceType(XdmAnyNodeType.Instance, ' ')
      };

      public override int
      MinimumNumberOfArguments => ArgumentTypes.Length;

      public override int
      MaximumNumberOfArguments => MinimumNumberOfArguments;

      public override ExtensionFunctionCall
      MakeFunctionCall() => new FunctionCall();

      public override XdmSequenceType
      ResultType(XdmSequenceType[] ArgumentTypes) =>
         new XdmSequenceType(XdmAtomicType.BuiltInAtomicType(QName.XS_INTEGER), ' ');

      class FunctionCall : ExtensionFunctionCall {

         public override IXdmEnumerator
         Call(IXdmEnumerator[] arguments, DynamicContext context) {

            XdmNode node = arguments[0].AsNodes().Single();

            return node.GetLineNumber()
               .ToXdmAtomicValue()
               .GetXdmEnumerator();
         }
      }
   }

   class LocalPathFunction : ExtensionFunctionDefinition {

      public override QName
      FunctionName { get; } = CompilerQName("_local-path");

      public override XdmSequenceType[]
      ArgumentTypes { get; } = {
         new XdmSequenceType(XdmAtomicType.BuiltInAtomicType(QName.XS_ANYURI), ' ')
      };

      public override int
      MinimumNumberOfArguments => ArgumentTypes.Length;

      public override int
      MaximumNumberOfArguments => MinimumNumberOfArguments;

      public override ExtensionFunctionCall
      MakeFunctionCall() => new FunctionCall();

      public override XdmSequenceType
      ResultType(XdmSequenceType[] ArgumentTypes) =>
         new XdmSequenceType(XdmAtomicType.BuiltInAtomicType(QName.XS_STRING), ' ');

      class FunctionCall : ExtensionFunctionCall {

         public override IXdmEnumerator
         Call(IXdmEnumerator[] arguments, DynamicContext context) {

            XdmAtomicValue value = arguments[0].AsAtomicValues().Single();

            Uri uri = value.Value as Uri
               ?? new Uri(value.ToString(), UriKind.RelativeOrAbsolute);

            return ExtensionFunctions.LocalPath(uri)
               .ToXdmAtomicValue()
               .GetXdmEnumerator();
         }
      }
   }

   class DocWithUrisFunction : ExtensionFunctionDefinition {

      readonly Processor
      processor;

      public override QName
      FunctionName { get; } = CompilerQName("_doc-with-uris");

      public override XdmSequenceType[]
      ArgumentTypes { get; } = {
         new XdmSequenceType(XdmAtomicType.BuiltInAtomicType(QName.XS_ANYURI), ' '),
         new XdmSequenceType(XdmAnyItemType.Instance, '+'),
         new XdmSequenceType(XdmAnyItemType.Instance, '?')
      };

      public override int
      MinimumNumberOfArguments => ArgumentTypes.Length;

      public override int
      MaximumNumberOfArguments => MinimumNumberOfArguments;

      public
      DocWithUrisFunction(Processor processor) {
         this.processor = processor;
      }

      public override ExtensionFunctionCall
      MakeFunctionCall() => new FunctionCall(this.processor);

      public override XdmSequenceType
      ResultType(XdmSequenceType[] ArgumentTypes) =>
         new XdmSequenceType(XdmAnyItemType.Instance, '+');

      class FunctionCall : ExtensionFunctionCall {

         Processor
         processor;

         public
         FunctionCall(Processor processor) {
            this.processor = processor;
         }

         public override void
         CopyLocalData(ExtensionFunctionCall destination) {
            ((FunctionCall)destination).processor = this.processor;
         }

         public override IXdmEnumerator
         Call(IXdmEnumerator[] arguments, DynamicContext context) {

            XdmAtomicValue value = arguments[0].AsAtomicValues().Single();

            XdmValue errorObject = new XdmValue(arguments[1].AsItems());
            var errorData = ModuleUriAndLineNumberFromErrorObject(errorObject);
            string? moduleUri = errorData.Item1;
            int lineNumber = errorData.Item2.GetValueOrDefault();

            XmlResolver? moduleResolver = arguments[2].AsItems()
               .Select(i => UnwrapExternalObject<XmlResolver>(i))
               .SingleOrDefault();

            Uri uri = (Uri)value.Value;

            if (!uri.IsAbsoluteUri) {
               throw new InvalidOperationException("Supplied URI must be absolute.");
            }

            var resolver = new LoggingResolver(GetModuleResolverOrDefault(moduleResolver));

            DocumentBuilder docb = this.processor.NewDocumentBuilder();
            docb.XmlResolver = resolver;

            XdmNode doc;

            try {
               doc = docb.Build(uri);

            } catch (FileNotFoundException) {

               throw new CompileException("Could not retrieve imported module.",
                  errorCode: "XTSE0165",
                  moduleUri: moduleUri,
                  lineNumber: lineNumber
               );

            } catch (XPathException ex) {

               var locator = ex.getLocator();

               throw new CompileException(ex.Message,
                  errorCode: ErrorCode(ex),
                  moduleUri: locator?.getSystemId() ?? uri?.AbsoluteUri,
                  lineNumber: locator?.getLineNumber() ?? -1
               );
            }

            return doc.Append(new XdmValue(resolver.ResolvedUris.Select(u => u.ToXdmAtomicValue())))
               .GetXdmEnumerator();
         }
      }
   }

   class PackageManifestFunction : ExtensionFunctionDefinition {

      readonly Processor
      processor;

      public override QName
      FunctionName { get; } = CompilerQName("_package-manifest");

      public override XdmSequenceType[]
      ArgumentTypes { get; } = {
         new XdmSequenceType(XdmAtomicType.BuiltInAtomicType(QName.XS_STRING), ' '),
         new XdmSequenceType(XdmAnyItemType.Instance, '?'),
         new XdmSequenceType(XdmAnyItemType.Instance, '+')
      };

      public override int
      MinimumNumberOfArguments => ArgumentTypes.Length;

      public override int
      MaximumNumberOfArguments => MinimumNumberOfArguments;

      public
      PackageManifestFunction(Processor processor) {
         this.processor = processor;
      }

      public override ExtensionFunctionCall
      MakeFunctionCall() => new FunctionCall(this.processor);

      public override XdmSequenceType
      ResultType(XdmSequenceType[] ArgumentTypes) =>
         new XdmSequenceType(XdmNodeKind.Document, '?');

      class FunctionCall : ExtensionFunctionCall {

         Processor
         processor;

         public
         FunctionCall(Processor processor) {
            this.processor = processor;
         }

         public override void
         CopyLocalData(ExtensionFunctionCall destination) {

            var call = (FunctionCall)destination;
            call.processor = this.processor;
         }

         public override IXdmEnumerator
         Call(IXdmEnumerator[] arguments, DynamicContext context) {

            string typeName = arguments[0]
               .AsAtomicValues()
               .Single()
               .ToString();

            Func<string, Type?> packageTypeResolver = arguments[1].AsItems()
               .Select(i => UnwrapExternalObject<Func<string, Type?>>(i))
               .SingleOrDefault()
               ?? ExtensionFunctions.ResolvePackageType;

            XdmValue errorObject = new XdmValue(arguments[2].AsItems());
            var errorData = ModuleUriAndLineNumberFromErrorObject(errorObject);
            string? moduleUri = errorData.Item1;
            int lineNumber = errorData.Item2.GetValueOrDefault();

            Type? packageType;
            const string errorCode = "XTSE3000";

            try {
               packageType = packageTypeResolver(typeName);

            } catch (Exception ex) {

               throw new CompileException(ex.Message,
                  errorCode: errorCode,
                  moduleUri: moduleUri,
                  lineNumber: lineNumber
               );
            }

            if (packageType is null) {
               return EmptyEnumerator.INSTANCE;
            }

            if (!PackageManifest.IsXcstPackage(packageType)) {

               throw new CompileException($"{packageType.FullName} is not a valid XCST package.",
                  errorCode: errorCode,
                  moduleUri: moduleUri,
                  lineNumber: lineNumber
               );
            }

            using (var output = new MemoryStream()) {

               using (XmlWriter writer = XmlWriter.Create(output)) {
                  PackageManifest.WriteManifest(packageType, writer);
               }

               output.Position = 0;

               DocumentBuilder builder = this.processor.NewDocumentBuilder();
               builder.BaseUri = new Uri(String.Empty, UriKind.Relative);

               XdmNode result = builder.Build(output);

               return result.GetXdmEnumerator();
            }
         }
      }
   }

   class PackageLocationFunction : ExtensionFunctionDefinition {

      public override QName
      FunctionName { get; } = CompilerQName("_package-location");

      public override XdmSequenceType[]
      ArgumentTypes { get; } = {
         new XdmSequenceType(XdmAtomicType.BuiltInAtomicType(QName.XS_STRING), ' '),
         new XdmSequenceType(XdmAnyItemType.Instance, '?'),
         new XdmSequenceType(XdmAtomicType.BuiltInAtomicType(QName.XS_ANYURI), '?'),
         new XdmSequenceType(XdmAtomicType.BuiltInAtomicType(QName.XS_STRING), '?'),
         new XdmSequenceType(XdmAtomicType.BuiltInAtomicType(QName.XS_STRING), '?')
      };

      public override int
      MinimumNumberOfArguments => ArgumentTypes.Length;

      public override int
      MaximumNumberOfArguments => MinimumNumberOfArguments;

      public override ExtensionFunctionCall
      MakeFunctionCall() => new FunctionCall();

      public override XdmSequenceType
      ResultType(XdmSequenceType[] ArgumentTypes) =>
         new XdmSequenceType(XdmAtomicType.BuiltInAtomicType(QName.XS_ANYURI), '?');

      class FunctionCall : ExtensionFunctionCall {

         public override IXdmEnumerator
         Call(IXdmEnumerator[] arguments, DynamicContext context) {

            string packageName = arguments[0].AsAtomicValues()
               .Single()
               .ToString();

            Func<string, Uri?>? packageLocationResolver = arguments[1].AsItems()
               .Select(i => UnwrapExternalObject<Func<string, Uri?>>(i))
               .SingleOrDefault();

            Uri? usingPackageUri = arguments[2].AsAtomicValues()
               .Select(i => i.Value as Uri ?? new Uri(i.ToString(), UriKind.RelativeOrAbsolute))
               .SingleOrDefault();

            string? packagesLocation = arguments[3].AsAtomicValues()
               .SingleOrDefault()?.ToString();

            string? packageFileExtension = arguments[4].AsAtomicValues()
               .SingleOrDefault()?.ToString();

            if (packagesLocation is null
               && usingPackageUri?.IsFile == true) {

               packagesLocation = Path.GetDirectoryName(usingPackageUri.LocalPath);
            }

            Uri? packageUri = null;

            if (packageLocationResolver != null) {
               packageUri = packageLocationResolver(packageName);

            } else if (!String.IsNullOrEmpty(packagesLocation)
               && !String.IsNullOrEmpty(packageFileExtension)) {

               packageUri = ExtensionFunctions.FindNamedPackage(packageName, packagesLocation!, packageFileExtension!);
            }

            return packageUri?.ToXdmAtomicValue()
               .GetXdmEnumerator()
               ?? EmptyEnumerator.INSTANCE;
         }
      }
   }

   class StringIdFunction : ExtensionFunctionDefinition {

      public override QName
      FunctionName { get; } = CompilerQName("_string-id");

      public override XdmSequenceType[]
      ArgumentTypes { get; } = {
         new XdmSequenceType(XdmAtomicType.BuiltInAtomicType(QName.XS_STRING), ' ')
      };

      public override int
      MinimumNumberOfArguments => ArgumentTypes.Length;

      public override int
      MaximumNumberOfArguments => MinimumNumberOfArguments;

      public override ExtensionFunctionCall
      MakeFunctionCall() => new FunctionCall();

      public override XdmSequenceType
      ResultType(XdmSequenceType[] ArgumentTypes) =>
         new XdmSequenceType(XdmAtomicType.BuiltInAtomicType(QName.XS_INTEGER), ' ');

      class FunctionCall : ExtensionFunctionCall {

         public override IXdmEnumerator
         Call(IXdmEnumerator[] arguments, DynamicContext context) {

            string str = arguments[0]
               .AsAtomicValues()
               .Single()
               .ToString();

            return ExtensionFunctions.StringId(str)
               .ToXdmAtomicValue()
               .GetXdmEnumerator();
         }
      }
   }

   class InvokeExternalFunctionFunction : ExtensionFunctionDefinition {

      public override QName
      FunctionName { get; } = CompilerQName("invoke-external-function");

      public override XdmSequenceType[]
      ArgumentTypes { get; } = {
         new XdmSequenceType(XdmAnyItemType.Instance, ' '),
         new XdmSequenceType(XdmAnyItemType.Instance, '*')
      };

      public override int
      MinimumNumberOfArguments => ArgumentTypes.Length;

      public override int
      MaximumNumberOfArguments => MinimumNumberOfArguments;

      public override ExtensionFunctionCall
      MakeFunctionCall() => new FunctionCall();

      public override XdmSequenceType
      ResultType(XdmSequenceType[] ArgumentTypes) =>
         new XdmSequenceType(XdmAnyItemType.Instance, '*');

      class FunctionCall : ExtensionFunctionCall {

         public override IXdmEnumerator
         Call(IXdmEnumerator[] arguments, DynamicContext context) {

            Delegate externalFunction = arguments[0].AsItems()
               .Cast<XdmExternalObjectValue>()
               .Select(x => UnwrapExternalObject<Delegate>(x))
               .Single();

            object[] functionArgs = arguments[1].AsItems()
               .Select(x => (x is XdmAtomicValue atomic) ? atomic.Value : x)
               .ToArray();

            object? result = externalFunction.DynamicInvoke(functionArgs);

            return result.ToXdmValue()
               .GetXdmEnumerator();
         }
      }
   }
}
