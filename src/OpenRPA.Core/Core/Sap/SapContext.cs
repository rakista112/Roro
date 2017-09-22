﻿// BSD 2-Clause License

// Copyright(c) 2017, Arvie Delgado
// All rights reserved.

// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:

// * Redistributions of source code must retain the above copyright notice, this
//   list of conditions and the following disclaimer.

// * Redistributions in binary form must reproduce the above copyright notice,
//   this list of conditions and the following disclaimer in the documentation
//   and/or other materials provided with the distribution.

// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
// AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
// IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED.IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE
// FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
// DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR
// SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
// CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY,
// OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

using OpenRPA.Queries;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace OpenRPA.Core
{
    public sealed class SapContext : Context
    {
        private XObject Application;

        public static readonly SapContext Shared = new SapContext();

        private SapContext()
        {
            this.Ensure();
        }

        public override Element GetElementFromFocus() =>
            this.Ensure()
            && this.Application.Get("ActiveSession") is XObject session
            && session.Get("ActiveWindow").Get("GuiFocus") is XObject rawElement
            ? new SapElement(rawElement) : null;

        public override Element GetElementFromPoint(int screenX, int screenY) =>
            this.Ensure()
            && this.Application.Get("ActiveSession") is XObject session
            && session.Invoke("FindByPosition", screenX, screenY, false) is XObject rawElementInfo
            && rawElementInfo.Invoke<string>("Item", 0) is string rawElementId
            && session.Invoke("FindById", rawElementId, false) is XObject rawElement
            ? new SapElement(rawElement) : null;

        public override IReadOnlyList<Element> GetElementsFromQuery(Query query)
        {
            var result = new List<SapElement>();
            var candidates = new Queue<SapElement>();
            var targetPath = query.First(x => x.Name == "Path").Value.ToString();

            var connections = this.Application.Get("Connections");
            for (var c = 0; c < connections.Get<int>("Count"); c++)
            {
                var sessions = connections.Invoke("Item", c).Get("Sessions");
                for (var s = 0; s < sessions.Get<int>("Count"); s++)
                {
                    var session = sessions.Invoke("Item", s);
                    candidates.Enqueue(new SapElement(session));
                }
            }

            while (candidates.Count > 0)
            {
                var candidate = candidates.Dequeue();
                var candidatePath = candidate.Path;
                if (targetPath.StartsWith(candidatePath))
                {
                    if (targetPath.Equals(candidatePath))
                    {
                        if (candidate.TryQuery(query))
                        {
                            result.Add(candidate);
                        }
                    }
                    else
                    {
                        foreach (var child in candidate.Children)
                        {
                            candidates.Enqueue(child);
                        }
                    }
                }
            }
            return result;
        }


        private bool Ensure()
        {
            if (this.ProcessId > 0 && Process.GetProcessById(this.ProcessId) is Process proc)
            {
                return true;
            }

            if (Type.GetTypeFromProgID("SapROTWr.SapROTWrapper") is Type type
                && new XObject(Activator.CreateInstance(type)) is XObject ROTWrapper
                && ROTWrapper.Invoke("GetROTEntry", "SAPGUI") is XObject ROTEntry
                && ROTEntry.Invoke("GetScriptingEngine") is XObject engine
                && Process.GetProcessesByName("saplogon").FirstOrDefault() is Process saplogon)
            {
                this.Application = engine;
                this.ProcessId = saplogon.Id;
                return true;
            }
            else
            {
                this.Application = null;
                this.ProcessId = 0;
                return false;
            }
        }
    }
}