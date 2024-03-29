﻿using NeuroSpeech.Eternity.Storage;
using System;
using System.Collections.Generic;

namespace NeuroSpeech.Eternity
{
    public class WorkflowStatus<T>
    {
        public EternityEntityState Status { get; set; }

        public T? Result { get; set; }

        public IDictionary<string,string> Extra { get; set; }

        public string? Error { get; set; }

        public DateTimeOffset DateCreated { get; set; }

        public DateTimeOffset LastUpdate { get; set; }
    }
}
