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
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Xml;
using Xcst.PackageModel;
using Xcst.Runtime;

namespace Xcst {

   public class XcstEvaluator {

      static readonly QualifiedName
      InitialTemplate = new QualifiedName("initial-template", XmlNamespaces.Xcst);

      readonly IXcstPackage
      package;

      readonly Dictionary<string, object?>
      parameters = new Dictionary<string, object?>();

      bool
      paramsLocked = false;

      PrimingContext?
      primingContext;

      public static XcstEvaluator
      Using(object package) {

         if (package is null) throw new ArgumentNullException(nameof(package));

         if (package is IXcstPackage pkg) {
            return new XcstEvaluator(pkg);
         }

         throw new ArgumentException("Provided instance is not a valid XCST package.", nameof(package));
      }

      public static XcstEvaluator<TPackage>
      Using<TPackage>(TPackage package) where TPackage : IXcstPackage {

         if (package is null) throw new ArgumentNullException(nameof(package));

         return new XcstEvaluator<TPackage>(package);
      }

      internal
      XcstEvaluator(IXcstPackage package) {

         if (package is null) throw new ArgumentNullException(nameof(package));

         this.package = package;
      }

      public XcstEvaluator
      WithParam(string name, object? value) {

         if (name is null) throw new ArgumentNullException(nameof(name));

         // since there's no way to un-prime, a second prime would still use the values
         // of the first prime (for parameters not specified in the second prime)
         // the workaround is to simply create a new evaluator

         if (this.paramsLocked) {
            throw new InvalidOperationException($"Cannot modify parameters, use a new {nameof(XcstEvaluator)} object.");
         }

         this.parameters[name] = value;

         return this;
      }

      public XcstEvaluator
      WithParams(object? parameters) {

         if (parameters != null) {
            WithParams(ObjectToDictionary(parameters));
         }

         return this;
      }

      public XcstEvaluator
      WithParams(IDictionary<string, object?>? parameters) {

         if (parameters != null) {

            foreach (var pair in parameters) {
               WithParam(pair.Key, pair.Value);
            }
         }

         return this;
      }

      internal static IDictionary<string, object?>
      ObjectToDictionary(object? values) {

         IDictionary<string, object?>? dict = null;

         if (values != null) {

            dict = values as IDictionary<string, object?>;

            if (dict != null) {
               return dict;
            }
         }

         if (dict is null) {
            dict = new Dictionary<string, object?>();
         }

         if (values != null) {

            PropertyDescriptorCollection props = TypeDescriptor.GetProperties(values);

            foreach (PropertyDescriptor prop in props) {
               object val = prop.GetValue(values);
               dict.Add(prop.Name, val);
            }
         }

         return dict;
      }

      public XcstTemplateEvaluator
      CallInitialTemplate() => CallTemplate(InitialTemplate);

      public XcstTemplateEvaluator
      CallTemplate(string name) {

         if (name is null) throw new ArgumentNullException(nameof(name));

         return CallTemplate(new QualifiedName(name));
      }

      public XcstTemplateEvaluator
      CallTemplate(QualifiedName name) {

         if (name is null) throw new ArgumentNullException(nameof(name));

         this.paramsLocked = true;

         return new XcstTemplateEvaluator(this.package, Prime, name);
      }

      internal PrimingContext
      Prime() {

         if (this.primingContext is null) {

            this.primingContext = PrimingContext.Create(this.parameters.Count);

            foreach (var param in this.parameters) {
               this.primingContext.WithParam(param.Key, param.Value);
            }

            this.parameters.Clear();
            this.package.Prime(this.primingContext, null);
         }

         return this.primingContext;
      }
   }

   public class XcstEvaluator<TPackage> : XcstEvaluator
         where TPackage : IXcstPackage {

      readonly TPackage
      package;

      internal
      XcstEvaluator(TPackage package)
         : base(package) {

         this.package = package;
      }

      public new XcstEvaluator<TPackage>
      WithParam(string name, object? value) {

         base.WithParam(name, value);
         return this;
      }

      public new XcstEvaluator<TPackage>
      WithParams(object? parameters) {

         base.WithParams(parameters);
         return this;
      }

      public new XcstEvaluator<TPackage>
      WithParams(IDictionary<string, object?>? parameters) {

         base.WithParams(parameters);
         return this;
      }

      public XcstOutputter
      CallFunction(Action<TPackage> functionCaller) {

         if (functionCaller is null) throw new ArgumentNullException(nameof(functionCaller));

         void executionFn(OutputParameters? overrideParams, bool skipFlush) =>
            functionCaller(this.package);

         return new XcstOutputter(this.package, Prime, executionFn);
      }

      public XcstOutputter<TResult>
      CallFunction<TResult>(Func<TPackage, TResult> functionCaller) {

         if (functionCaller is null) throw new ArgumentNullException(nameof(functionCaller));

         TResult executionFn() => functionCaller(this.package);

         return new XcstOutputter<TResult>(this.package, Prime, executionFn);
      }
   }

   public class XcstTemplateEvaluator {

      readonly IXcstPackage
      package;

      readonly Func<PrimingContext>
      primeFn;

      readonly QualifiedName
      name;

      readonly Dictionary<string, object?>
      templateParameters = new Dictionary<string, object?>();

      readonly Dictionary<string, object?>
      tunnelParameters = new Dictionary<string, object?>();

      internal
      XcstTemplateEvaluator(IXcstPackage package, Func<PrimingContext> primeFn, QualifiedName name) {

         if (package is null) throw new ArgumentNullException(nameof(package));
         if (primeFn is null) throw new ArgumentNullException(nameof(primeFn));
         if (name is null) throw new ArgumentNullException(nameof(name));

         this.package = package;
         this.primeFn = primeFn;
         this.name = name;
      }

      public XcstTemplateEvaluator
      WithParam(string name, object? value, bool tunnel = false) {

         if (name is null) throw new ArgumentNullException(nameof(name));

         if (tunnel) {
            this.tunnelParameters[name] = value;
         } else {
            this.templateParameters[name] = value;
         }

         return this;
      }

      public XcstTemplateEvaluator
      WithParams(object? parameters) {

         if (parameters != null) {
            WithParams(XcstEvaluator.ObjectToDictionary(parameters));
         }

         return this;
      }

      public XcstTemplateEvaluator
      WithParams(IDictionary<string, object?>? parameters) {

         if (parameters != null) {

            foreach (var pair in parameters) {
               WithParam(pair.Key, pair.Value);
            }
         }

         return this;
      }

      public XcstTemplateEvaluator
      WithTunnelParams(object? parameters) {

         if (parameters != null) {
            WithTunnelParams(XcstEvaluator.ObjectToDictionary(parameters));
         }

         return this;
      }

      public XcstTemplateEvaluator
      WithTunnelParams(IDictionary<string, object?>? parameters) {

         if (parameters != null) {

            foreach (var pair in parameters) {
               WithParam(pair.Key, pair.Value, tunnel: true);
            }
         }

         return this;
      }

      public XcstTemplateEvaluator
      ClearParams() {

         this.templateParameters.Clear();
         this.tunnelParameters.Clear();
         return this;
      }

      public XcstOutputter
      OutputTo(Uri file) {

         if (file is null) throw new ArgumentNullException(nameof(file));
         if (!file.IsAbsoluteUri) throw new ArgumentException("file must be an absolute URI.", nameof(file));
         if (!file.IsFile) throw new ArgumentException("file must be a file URI", nameof(file));

         return CreateOutputter(WriterFactory.CreateWriter(file));
      }

      public XcstOutputter
      OutputTo(Stream output) {

         if (output is null) throw new ArgumentNullException(nameof(output));

         return CreateOutputter(WriterFactory.CreateWriter(output, null));
      }

      public XcstOutputter
      OutputTo(TextWriter output) {

         if (output is null) throw new ArgumentNullException(nameof(output));

         return CreateOutputter(WriterFactory.CreateWriter(output, null));
      }

      public XcstOutputter
      OutputTo(XmlWriter output) {

         if (output is null) throw new ArgumentNullException(nameof(output));

         return CreateOutputter(WriterFactory.CreateWriter(output, null));
      }

      public XcstOutputter
      OutputTo(XcstWriter output) {

         if (output is null) throw new ArgumentNullException(nameof(output));

         return CreateOutputter(WriterFactory.CreateWriter(output));
      }

      public XcstOutputter
      OutputTo<TItem>(ICollection<TItem> output) {

         if (output is null) throw new ArgumentNullException(nameof(output));

         var seqWriter = new SequenceWriter<TItem>(output);

         return OutputToRaw(seqWriter);
      }

      /// <exclude/>
      [EditorBrowsable(EditorBrowsableState.Never)]
      public XcstOutputter
      OutputToRaw<TBase>(ISequenceWriter<TBase> output) {

         if (output is null) throw new ArgumentNullException(nameof(output));

         Action<TemplateContext> tmplFn = this.package.GetTemplate<TBase>(this.name, output);

         void executionFn(OutputParameters? overrideParams, bool skipFlush, TemplateContext tmplContext) =>
            tmplFn(tmplContext);

         return CreateOutputter(executionFn);
      }

      XcstOutputter
      CreateOutputter(CreateWriterDelegate writerFn) {

         void executionFn(OutputParameters? overrideParams, bool skipFlush, TemplateContext tmplContext) =>
            EvaluateToWriter(writerFn, overrideParams, skipFlush, tmplContext);

         return CreateOutputter(executionFn);
      }

      XcstOutputter
      CreateOutputter(Action<OutputParameters?, bool, TemplateContext> executionFn) {

         // it's important to keep template parameters' variables outside the execution delegate
         // so subsequent modifications do not affect previously created outputters

         var templateParams = new Dictionary<string, object?>(this.templateParameters);
         var tunnelParams = new Dictionary<string, object?>(this.tunnelParameters);

         void executionFn2(OutputParameters? overrideParams, bool skipFlush) =>
            executionFn(overrideParams, skipFlush, CreateTemplateContext(templateParams, tunnelParams));

         return new XcstOutputter(this.package, this.primeFn, executionFn2);
      }

      static TemplateContext
      CreateTemplateContext(Dictionary<string, object?> templateParams, Dictionary<string, object?> tunnelParams) {

         var context = TemplateContext.Create(templateParams.Count, tunnelParams.Count);

         foreach (var param in templateParams) {
            context.WithParam(param.Key, param.Value);
         }

         foreach (var param in tunnelParams) {
            context.WithParam(param.Key, param.Value, tunnel: true);
         }

         return context;
      }

      void
      EvaluateToWriter(CreateWriterDelegate writerFn, OutputParameters? overrideParams, bool skipFlush, TemplateContext tmplContext) {

         var defaultParams = new OutputParameters();
         this.package.ReadOutputDefinition(null, defaultParams);

         RuntimeWriter writer = writerFn(defaultParams, overrideParams, this.package.Context);

         try {

            Action<TemplateContext> tmplFn = this.package.GetTemplate(this.name, writer);
            tmplFn(tmplContext);

            if (!writer.DisposeWriter
               && !skipFlush) {

               writer.Flush();
            }

         } finally {

            if (writer.DisposeWriter) {
               writer.Dispose();
            }
         }
      }
   }

   public class XcstOutputter {

      readonly IXcstPackage
      package;

      readonly Func<PrimingContext>
      primeFn;

      readonly Action<OutputParameters?, bool>
      executionFn;

      OutputParameters?
      parameters;

      Func<IFormatProvider>?
      formatProviderFn;

      Uri?
      baseUri;

      Uri?
      baseOutputUri;

      internal
      XcstOutputter(IXcstPackage package, Func<PrimingContext> primeFn, Action<OutputParameters?, bool> executionFn) {

         if (package is null) throw new ArgumentNullException(nameof(package));
         if (primeFn is null) throw new ArgumentNullException(nameof(primeFn));
         if (executionFn is null) throw new ArgumentNullException(nameof(executionFn));

         this.package = package;
         this.primeFn = primeFn;
         this.executionFn = executionFn;
      }

      public XcstOutputter
      WithParams(OutputParameters? parameters) {

         if (parameters != null) {
            parameters = new OutputParameters(parameters);
         }

         this.parameters = parameters;
         return this;
      }

      public XcstOutputter
      WithFormatProvider(IFormatProvider? formatProvider) {

         if (formatProvider != null) {
            return WithFormatProvider(() => formatProvider);
         }

         this.formatProviderFn = null;
         return this;
      }

      public XcstOutputter
      WithFormatProvider(Func<IFormatProvider>? formatProviderFn) {

         this.formatProviderFn = formatProviderFn;
         return this;
      }

      public XcstOutputter
      WithBaseUri(Uri? baseUri) {

         if (baseUri != null
            && !baseUri.IsAbsoluteUri) {

            throw new ArgumentException("An absolute URI is expected.", nameof(baseUri));
         }

         this.baseUri = baseUri;
         return this;
      }

      public XcstOutputter
      WithBaseOutputUri(Uri? baseOutputUri) {

         if (baseOutputUri != null
            && !baseOutputUri.IsAbsoluteUri) {

            throw new ArgumentException("An absolute URI is expected.", nameof(baseOutputUri));
         }

         this.baseOutputUri = baseOutputUri;
         return this;
      }

      public void
      Run(bool skipFlush = false) {

         InitPackage();
         this.executionFn(this.parameters, skipFlush);
      }

      internal void
      InitPackage() {

         PrimingContext primingContext = this.primeFn();

         var execContext = new ExecutionContext(this.package, primingContext, this.formatProviderFn, this.baseUri, this.baseOutputUri);

         this.package.Context = execContext;
      }
   }

   public class XcstOutputter<TResult> : XcstOutputter {

      readonly Func<TResult>
      executionFn;

      internal
      XcstOutputter(IXcstPackage package, Func<PrimingContext> primeFn, Func<TResult> executionFn)
         : base(package, primeFn, (p, sf) => executionFn()) {

         this.executionFn = executionFn;
      }

      public new XcstOutputter<TResult>
      WithParams(OutputParameters? parameters) {

         base.WithParams(parameters);
         return this;
      }

      public new XcstOutputter<TResult>
      WithFormatProvider(IFormatProvider? formatProvider) {

         base.WithFormatProvider(formatProvider);
         return this;
      }

      public new XcstOutputter<TResult>
      WithFormatProvider(Func<IFormatProvider>? formatProviderFn) {

         base.WithFormatProvider(formatProviderFn);
         return this;
      }

      public new XcstOutputter<TResult>
      WithBaseUri(Uri? baseUri) {

         base.WithBaseUri(baseUri);
         return this;
      }

      public new XcstOutputter<TResult>
      WithBaseOutputUri(Uri? baseOutputUri) {

         base.WithBaseOutputUri(baseOutputUri);
         return this;
      }

      public TResult
      Evaluate() {

         InitPackage();
         return this.executionFn();
      }
   }
}
