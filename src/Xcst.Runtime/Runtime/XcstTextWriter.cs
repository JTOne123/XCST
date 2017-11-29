﻿// Copyright 2017 Max Toro Q.
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
using System.Text;

namespace Xcst.Runtime {

   class XcstTextWriter : TextWriter {

      readonly XcstWriter baseWriter;

      public override Encoding Encoding {
         get { throw new NotImplementedException(); }
      }

      public XcstTextWriter(XcstWriter baseWriter)
         : base(baseWriter.SimpleContent.FormatProvider) {

         this.baseWriter = baseWriter;
      }

      public override void Write(char value) {
         this.baseWriter.WriteString(value.ToString());
      }

      public override void Write(string value) {
         this.baseWriter.WriteString(value);
      }

      public override void Write(char[] buffer, int index, int count) {
         this.baseWriter.WriteChars(buffer, index, count);
      }
   }
}