﻿namespace Microsoft.Pc.Parser
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;
    using System.Linq;
    using QUT.Gppg;

    using Domains;
    using Microsoft.Formula.API;
    using Microsoft.Pc;
    using Microsoft.Formula.API.Generators;
    using Microsoft.Formula.API.Nodes;
    

    internal partial class PParser : ShiftReduceParser<LexValue, LexLocation>
    {
        private static readonly P_Root.Exprs TheDefaultExprs = new P_Root.Exprs();

        private ProgramName parseSource;
        private List<Flag> parseFlags;
        private PProgram parseProgram;
        private List<string> parseIncludedFileNames;

        private bool parseFailed = false;

        private Span crntAnnotSpan;
        private bool isTrigAnnotated = false;
        private bool isFunProtoDecl = false;
        private P_Root.FunDecl crntFunDecl = null;
        private P_Root.FunProtoDecl crntFunProtoDecl = null;
        private P_Root.EventDecl crntEventDecl = null;
        private P_Root.MachineDecl crntMachDecl = null;
        private P_Root.MachineProtoDecl crntMachProtoDecl = null;
        private P_Root.InterfaceTypeDecl crntInterfaceDecl = null;
        private P_Root.QualifiedName crntStateTargetName = null;
        private P_Root.QualifiedName crntGotoTargetName = null;
        private P_Root.StateDecl crntState = null;
        private List<P_Root.VarDecl> crntVarList = new List<P_Root.VarDecl>();
        private List<P_Root.EventName> crntEventList = new List<P_Root.EventName>();
        private List<P_Root.EventName> onEventList = new List<P_Root.EventName>();
        private List<P_Root.String> crntStringIdList = new List<P_Root.String>();
        private List<Tuple<P_Root.StringCnst, P_Root.AnnotValue>> crntAnnotList = new List<Tuple<P_Root.StringCnst, P_Root.AnnotValue>>();
        private Stack<List<Tuple<P_Root.StringCnst, P_Root.AnnotValue>>> crntAnnotStack = new Stack<List<Tuple<P_Root.StringCnst, P_Root.AnnotValue>>>();

        private List<P_Root.EventName> crntObservesList = new List<P_Root.EventName>();

        private int anonEventSetCounter = 0;
        private HashSet<string> crntStateNames = new HashSet<string>();
        private HashSet<string> crntFunNames = new HashSet<string>();
        private HashSet<string> crntVarNames = new HashSet<string>();

        private TopDeclNames topDeclNames;

        private Stack<P_Root.Expr> valueExprStack = new Stack<P_Root.Expr>();
        private Stack<P_Root.ExprsExt> exprsStack = new Stack<P_Root.ExprsExt>();
        private Stack<P_Root.TypeExpr> typeExprStack = new Stack<P_Root.TypeExpr>();
        private Stack<P_Root.Stmt> stmtStack = new Stack<P_Root.Stmt>();
        private Stack<P_Root.QualifiedName> groupStack = new Stack<P_Root.QualifiedName>();
        private int nextTrampolineLabel = 0;
        private int nextPayloadVarLabel = 0;

        class LocalVarStack
        {
            private PParser parser;

            private P_Root.IArgType_NmdTupType__1 contextLocalVarDecl;
            public P_Root.IArgType_NmdTupType__1 ContextLocalVarDecl
            {
                get
                {
                    Contract.Assert(0 < contextStack.Count);
                    return Reverse(contextStack.Peek());
                }
            }
            private Stack<P_Root.IArgType_NmdTupType__1> contextStack;

            private List<P_Root.StringCnst> crntLocalVarList;
            private P_Root.IArgType_NmdTupType__1 localVarDecl;
            public P_Root.IArgType_NmdTupType__1 LocalVarDecl
            {
                get { return Reverse(localVarDecl); }
            }
            private Stack<P_Root.IArgType_NmdTupType__1> localStack;

            private Stack<List<P_Root.EventName>> caseEventStack;

            private P_Root.IArgType_Cases__2 casesList;
            private Stack<P_Root.IArgType_Cases__2> casesListStack;

            public LocalVarStack(PParser parser)
            {
                this.parser = parser;
                this.contextLocalVarDecl = P_Root.MkUserCnst(P_Root.UserCnstKind.NIL);
                this.contextStack = new Stack<P_Root.IArgType_NmdTupType__1>();
                this.crntLocalVarList = new List<P_Root.StringCnst>();
                this.localVarDecl = P_Root.MkUserCnst(P_Root.UserCnstKind.NIL);
                this.localStack = new Stack<P_Root.IArgType_NmdTupType__1>();
                this.caseEventStack = new Stack<List<P_Root.EventName>>();
                this.casesList = P_Root.MkUserCnst(P_Root.UserCnstKind.NIL);
                this.casesListStack = new Stack<P_Root.IArgType_Cases__2>();
            }

            public LocalVarStack(PParser parser, P_Root.IArgType_NmdTupType__1 parameters)
            {
                this.parser = parser;
                this.contextLocalVarDecl = Reverse(parameters);
                this.contextStack = new Stack<P_Root.IArgType_NmdTupType__1>();
                this.crntLocalVarList = new List<P_Root.StringCnst>();
                this.localVarDecl = P_Root.MkUserCnst(P_Root.UserCnstKind.NIL);
                this.localStack = new Stack<P_Root.IArgType_NmdTupType__1>();
                this.caseEventStack = new Stack<List<P_Root.EventName>>();
                this.casesList = P_Root.MkUserCnst(P_Root.UserCnstKind.NIL);
                this.casesListStack = new Stack<P_Root.IArgType_Cases__2>();
            }

            public void PushCasesList()
            {
                casesListStack.Push(casesList);
                casesList = P_Root.MkUserCnst(P_Root.UserCnstKind.NIL);
            }

            public P_Root.IArgType_Cases__2 PopCasesList()
            {
                var currCasesList = casesList;
                casesList = casesListStack.Pop();
                return currCasesList;
            }

            private P_Root.IArgType_NmdTupType__1 Reverse(P_Root.IArgType_NmdTupType__1 list)
            {
                P_Root.IArgType_NmdTupType__1 reverseList = P_Root.MkUserCnst(P_Root.UserCnstKind.NIL);
                var iter = list;
                while (true)
                {
                    var next = iter as P_Root.NmdTupType;
                    if (next == null) break;
                    var nmdTupType = P_Root.MkNmdTupType();
                    nmdTupType.hd = next.hd;
                    nmdTupType.tl = reverseList;
                    reverseList = nmdTupType;
                    iter = next.tl;
                }
                return reverseList;
            }

            public void Push()
            {
                contextStack.Push(contextLocalVarDecl);
                localStack.Push(localVarDecl);
                List<P_Root.EventName> caseEventList = new List<P_Root.EventName>(parser.crntEventList);
                parser.crntEventList.Clear();
                caseEventStack.Push(caseEventList);
                localVarDecl = P_Root.MkUserCnst(P_Root.UserCnstKind.NIL);
            }

            public List<P_Root.EventName> Pop()
            {
                contextLocalVarDecl = contextStack.Pop();
                contextLocalVarDecl = ((P_Root.NmdTupType)contextLocalVarDecl).tl;
                localVarDecl = localStack.Pop();
                return caseEventStack.Pop();
            }

            public void AddCase(P_Root.IArgType_Cases__0 e, P_Root.IArgType_Cases__1 a, Span caseSpan)
            {
                casesList = P_Root.MkCases(e, a, casesList);
                casesList.Span = caseSpan;
            }

            public void AddPayloadVar(P_Root.UserCnstKind qualKind, string name, Span span)
            {
                Contract.Assert(parser.typeExprStack.Count > 0);
                var typeExpr = (P_Root.IArgType_NmdTupTypeField__2)parser.typeExprStack.Pop();
                var nameTerm = P_Root.MkString(name);
                nameTerm.Span = span;
                var field = P_Root.MkNmdTupTypeField(P_Root.MkUserCnst(qualKind), nameTerm, typeExpr);
                contextLocalVarDecl = P_Root.MkNmdTupType(field, contextLocalVarDecl);
            }

            public void AddPayloadVar(P_Root.UserCnstKind qualKind)
            {
                var field = P_Root.MkNmdTupTypeField(
                                    P_Root.MkUserCnst(qualKind),
                                    P_Root.MkString(string.Format("_payload_{0}", parser.GetNextPayloadVarLabel())), 
                                    (P_Root.IArgType_NmdTupTypeField__2) parser.MkBaseType(P_Root.UserCnstKind.NULL, Span.Unknown));
                contextLocalVarDecl = P_Root.MkNmdTupType(field, contextLocalVarDecl);
            }

            public void AddLocalVar(string name, Span span)
            {
                var nameTerm = P_Root.MkString(name);
                nameTerm.Span = span;
                crntLocalVarList.Add(nameTerm);
            }

            public void CompleteCrntLocalVarList()
            {
                Contract.Assert(parser.typeExprStack.Count > 0);
                var typeExpr = (P_Root.IArgType_NmdTupTypeField__2)parser.typeExprStack.Pop();
                foreach (var v in crntLocalVarList)
                {
                    var field = P_Root.MkNmdTupTypeField(P_Root.MkUserCnst(P_Root.UserCnstKind.NONE), v, typeExpr);
                    localVarDecl = P_Root.MkNmdTupType(field, localVarDecl);
                    contextLocalVarDecl = P_Root.MkNmdTupType(field, contextLocalVarDecl);
                }
                crntLocalVarList.Clear();
            }
        }

        LocalVarStack localVarStack;

        public P_Root.TypeExpr Debug_PeekTypeStack
        {
            get { return typeExprStack.Peek(); }
        }

        public PParser()
            : base(new Scanner())
        {
            localVarStack = new LocalVarStack(this);
        }

        CommandLineOptions Options;

        P_Root.Id MkId(Span span)
        {
            return MkUserCnst(P_Root.UserCnstKind.NIL, span);
        }

        P_Root.Id MkId(Span entrySpan, Span exitSpan)
        {
            return MkUserCnst(P_Root.UserCnstKind.NIL, entrySpan);
        }

        internal bool ParseFile(
            ProgramName file,
            CommandLineOptions options,
            TopDeclNames topDeclNames,
            PProgram program,
            out List<Flag> flags,
            out List<string> includedFileNames)
        {
            flags = parseFlags = new List<Flag>();
            this.topDeclNames = topDeclNames;
            parseProgram = program;
            includedFileNames = parseIncludedFileNames = new List<string>();
            parseSource = file;
            Options = options;
            bool result;
            try
            {
                var fi = new System.IO.FileInfo(file.Uri.LocalPath);
                if (!fi.Exists)
                {
                    var badFile = new Flag(
                        SeverityKind.Error,
                        default(Span),
                        Constants.BadFile.ToString(string.Format("The file {0} does not exist", fi.FullName)),
                        Constants.BadFile.Code,
                        file);
                    result = false;
                    flags.Add(badFile);
                    return false;
                }

                var str = new System.IO.FileStream(file.Uri.LocalPath, System.IO.FileMode.Open, System.IO.FileAccess.Read);
                var scanner = ((Scanner)Scanner);
                scanner.SetSource(str);
                scanner.SourceProgram = file;
                scanner.Flags = flags;
                scanner.Failed = false;
                ResetState();
                result = (!scanner.Failed) && Parse(default(System.Threading.CancellationToken)) && !parseFailed;
                str.Close();
            }
            catch (Exception e)
            {
                var badFile = new Flag(
                    SeverityKind.Error,
                    default(Span),
                    Constants.BadFile.ToString(e.Message),
                    Constants.BadFile.Code,
                    file);
                flags.Add(badFile);
                return false;
            }

            return result;
        }

        private Span ToSpan(LexLocation loc)
        {
            return new Span(loc.StartLine, loc.StartColumn + 1, loc.EndLine, loc.EndColumn + 1, this.parseSource);
        }


        public bool IsValidName(TopDecl type, string name, Span nameSpan)
        {
            string errorMessage = "";
            bool error = false;
            switch (type)
            {
                case TopDecl.Event:
                    if (topDeclNames.eventNames.Contains(name))
                    {
                        errorMessage = string.Format("An event with name {0} already declared", name);
                        error = true;
                    }
                    break;
                case TopDecl.EventSet:
                    if (topDeclNames.eventSetNames.Contains(name))
                    {
                        errorMessage = string.Format("A event set with name {0} already declared", name);
                        error = true;
                    }
                    break;
                case TopDecl.Interface:
                case TopDecl.Enum:
                case TopDecl.TypeDef:
                    if (topDeclNames.interfaceNames.Contains(name))
                    {
                        errorMessage = string.Format("A interface with name {0} already declared", name);
                        error = true;
                    }
                    else if (topDeclNames.typeNames.Contains(name))
                    {
                        errorMessage = string.Format("A typedef with name {0} already declared", name);
                        error = true;
                    }
                    else if(topDeclNames.enumNames.Contains(name))
                    {
                        errorMessage = string.Format("An enum with name {0} already declared", name);
                        error = true;
                    }
                    break;
                case TopDecl.Machine:
                    if (topDeclNames.machineNames.Contains(name))
                    {
                        errorMessage = string.Format("A machine with name {0} already declared", name);
                        error = true;
                    }
                    else if(topDeclNames.interfaceNames.Contains(name))
                    {
                        errorMessage = string.Format("A interface with name {0} already declared", name);
                        error = true;
                    }
                    break;
                case TopDecl.Module:
                    if (topDeclNames.moduleNames.Contains(name))
                    {
                        errorMessage = string.Format("A module with name {0} already declared", name);
                        error = true;
                    }
                    break;
                case TopDecl.Test:
                    if (topDeclNames.testNames.Contains(name))
                    {
                        errorMessage = string.Format("A test case with name {0} already declared", name);
                        error = true;
                    }
                    break;
                case TopDecl.MachineProto:
                    if (topDeclNames.machineProto.Contains(name))
                    {
                        errorMessage = string.Format("A machine prototype with name {0} already declared", name);
                        error = true;
                    }
                    break;
                case TopDecl.FunProto:
                    if (topDeclNames.funProto.Contains(name))
                    {
                        errorMessage = string.Format("A function prototype with name {0} already declared", name);
                        error = true;
                    }
                    break;
            }

            if (error)
            {
                var errFlag = new Flag(
                                         SeverityKind.Error,
                                         nameSpan,
                                         Constants.BadSyntax.ToString(errorMessage),
                                         Constants.BadSyntax.Code,
                                         parseSource);
                parseFailed = true;
                parseFlags.Add(errFlag);
            }

            return !error;

        }

        #region Pushers
        private void PushAnnotationSet()
        {
            crntAnnotStack.Push(crntAnnotList);
            crntAnnotList = new List<Tuple<P_Root.StringCnst, P_Root.AnnotValue>>();
        }

        private void PushTypeExpr(P_Root.TypeExpr typeExpr)
        {
            typeExprStack.Push(typeExpr);
        }

        private void PushNameType(string name, Span span)
        {
            var nameType = P_Root.MkNameType(MkString(name, span));
            nameType.Span = span;
            typeExprStack.Push(nameType);
        }

        private void PushSeqType(Span span)
        {
            Contract.Assert(typeExprStack.Count > 0);
            var seqType = P_Root.MkSeqType((P_Root.IArgType_SeqType__0)typeExprStack.Pop());
            seqType.Span = span;
            typeExprStack.Push(seqType);
        }

        private void PushTupType(Span span, bool isLast)
        {
            var tupType = P_Root.MkTupType();
            tupType.Span = span;
            if (isLast)
            {
                Contract.Assert(typeExprStack.Count > 0);
                tupType.hd = (P_Root.IArgType_TupType__0)typeExprStack.Pop();
                tupType.tl = MkUserCnst(P_Root.UserCnstKind.NIL, span);
            }
            else
            {
                Contract.Assert(typeExprStack.Count > 1);
                tupType.tl = (P_Root.IArgType_TupType__1)typeExprStack.Pop();
                tupType.hd = (P_Root.IArgType_TupType__0)typeExprStack.Pop();
            }

            typeExprStack.Push(tupType);
        }

        Stack<P_Root.UserCnst> qualifier = new Stack<P_Root.UserCnst>();

        private void PushNmdTupType(string fieldName, Span span, bool isLast)
        {
            var tupType = P_Root.MkNmdTupType();
            var tupFld = P_Root.MkNmdTupTypeField();

            tupType.Span = span;
            tupFld.Span = span;
            tupFld.qual = qualifier.Pop();
            tupFld.name = MkString(fieldName, span);
            tupType.hd = tupFld;
            if (isLast)
            {
                Contract.Assert(typeExprStack.Count > 0);
                tupFld.type = (P_Root.IArgType_NmdTupTypeField__2)typeExprStack.Pop();
                tupType.tl = MkUserCnst(P_Root.UserCnstKind.NIL, span);
            }
            else
            {
                Contract.Assert(typeExprStack.Count > 1);
                tupType.tl = (P_Root.IArgType_NmdTupType__1)typeExprStack.Pop();
                tupFld.type = (P_Root.IArgType_NmdTupTypeField__2)typeExprStack.Pop();
            }

            typeExprStack.Push(tupType);
        }

        private void PushMapType(Span span)
        {
            Contract.Assert(typeExprStack.Count > 1);
            var mapType = P_Root.MkMapType();
            mapType.v = (P_Root.IArgType_MapType__1)typeExprStack.Pop();
            mapType.k = (P_Root.IArgType_MapType__0)typeExprStack.Pop();
            mapType.Span = span;
            typeExprStack.Push(mapType);
        }

        private void PushSend(bool hasArgs, Span span)
        {
            Contract.Assert(!hasArgs || exprsStack.Count > 0);
            Contract.Assert(valueExprStack.Count > 1);

            var sendStmt = P_Root.MkSend();
            sendStmt.Span = span;
            sendStmt.id = (P_Root.IArgType_Send__4) MkId(span);
            if (hasArgs)
            {
                var arg = exprsStack.Pop();
                if (arg.Symbol == TheDefaultExprs.Symbol)
                {
                    sendStmt.arg = P_Root.MkTuple((P_Root.IArgType_Tuple__0)arg);
                    sendStmt.arg.Span = arg.Span;
                }
                else
                {
                    sendStmt.arg = (P_Root.IArgType_Send__2)arg;
                }
                sendStmt.ev = (P_Root.IArgType_Send__1)valueExprStack.Pop();
                sendStmt.dest = (P_Root.IArgType_Send__0)valueExprStack.Pop();
            }
            else
            {
                sendStmt.ev = (P_Root.IArgType_Send__1)valueExprStack.Pop();
                sendStmt.dest = (P_Root.IArgType_Send__0)valueExprStack.Pop();
                sendStmt.arg = MkUserCnst(P_Root.UserCnstKind.NIL, span);
            }
            sendStmt.qual = qualifier.Pop();

            stmtStack.Push(sendStmt);
        }

        private void PushGoto(bool hasArgs, Span span)
        {
            Contract.Assert(crntGotoTargetName != null);
            Contract.Assert(!hasArgs || exprsStack.Count > 0);

            var gotoStmt = P_Root.MkGoto();
            gotoStmt.dst = crntGotoTargetName;
            gotoStmt.Span = span;
            gotoStmt.id = (P_Root.IArgType_Goto__2) MkId(span);
            if (hasArgs)
            {
                var arg = exprsStack.Pop();
                if (arg.Symbol == TheDefaultExprs.Symbol)
                {
                    gotoStmt.arg = P_Root.MkTuple((P_Root.IArgType_Tuple__0)arg);
                    gotoStmt.arg.Span = arg.Span;
                }
                else
                {
                    gotoStmt.arg = (P_Root.IArgType_Goto__1)arg;
                }
            }
            else
            {
                gotoStmt.arg = MkUserCnst(P_Root.UserCnstKind.NIL, span);
            }
            stmtStack.Push(gotoStmt);
            crntGotoTargetName = null;
        }

        private void PushAnnounce(bool hasArgs, string name, Span span)
        {
            Contract.Assert(!hasArgs || exprsStack.Count > 0);
            Contract.Assert(valueExprStack.Count > 0);

            var announceStmt = P_Root.MkAnnounce();
            announceStmt.Span = span;
            announceStmt.id = (P_Root.IArgType_Announce__2)MkId(span);
            if (hasArgs)
            {
                var arg = exprsStack.Pop();
                if (arg.Symbol == TheDefaultExprs.Symbol)
                {
                    announceStmt.arg = P_Root.MkTuple((P_Root.IArgType_Tuple__0)arg);
                    announceStmt.arg.Span = arg.Span;
                }
                else
                {
                    announceStmt.arg = (P_Root.IArgType_Announce__1)arg;
                }

                announceStmt.ev = (P_Root.IArgType_Announce__0)valueExprStack.Pop();
            }
            else
            {
                announceStmt.ev = (P_Root.IArgType_Announce__0)valueExprStack.Pop();
                announceStmt.arg = MkUserCnst(P_Root.UserCnstKind.NIL, span);
            }

            if (Options.test)
            {
                stmtStack.Push(announceStmt);
            }
            else
            {
                var skipStmt = P_Root.MkNulStmt(MkUserCnst(P_Root.UserCnstKind.SKIP, span));
                skipStmt.id = (P_Root.IArgType_NulStmt__1) MkId(span);
                stmtStack.Push(skipStmt);
            }
        }

        private void PushReceive(Span span)
        {
            var receiveStmt = P_Root.MkReceive((P_Root.IArgType_Receive__0)localVarStack.PopCasesList());
            receiveStmt.Span = span;
            receiveStmt.label = P_Root.MkNumeric(GetNextTrampolineLabel());
            receiveStmt.id = (P_Root.IArgType_Receive__2) MkId(span);
            stmtStack.Push(receiveStmt);
        }

        private void PushRaise(bool hasArgs, Span span)
        {
            Contract.Assert(!hasArgs || exprsStack.Count > 0);
            Contract.Assert(valueExprStack.Count > 0);

            var raiseStmt = P_Root.MkRaise();
            raiseStmt.Span = span;
            raiseStmt.id = (P_Root.IArgType_Raise__2) MkId(span);
            if (hasArgs)
            {
                var arg = exprsStack.Pop();
                if (arg.Symbol == TheDefaultExprs.Symbol)
                {
                    raiseStmt.arg = P_Root.MkTuple((P_Root.IArgType_Tuple__0)arg);
                    raiseStmt.arg.Span = arg.Span;
                }
                else
                {
                    raiseStmt.arg = (P_Root.IArgType_Raise__1)arg;
                }

                raiseStmt.ev = (P_Root.IArgType_Raise__0)valueExprStack.Pop();
            }
            else
            {
                raiseStmt.ev = (P_Root.IArgType_Raise__0)valueExprStack.Pop();
                raiseStmt.arg = MkUserCnst(P_Root.UserCnstKind.NIL, span);
            }

            stmtStack.Push(raiseStmt);
        }

        private void PushNewStmt(string name, Span nameSpan, bool hasArgs, Span span)
        {
            Contract.Assert(!hasArgs || exprsStack.Count > 0);
            var newStmt = P_Root.MkNewStmt();
            newStmt.name = MkString(name, nameSpan);
            newStmt.Span = span;
            newStmt.id = (P_Root.IArgType_NewStmt__2) MkId(span);
            if (hasArgs)
            {
                var arg = exprsStack.Pop();
                if (arg.Symbol == TheDefaultExprs.Symbol)
                {
                    newStmt.arg = P_Root.MkTuple((P_Root.IArgType_Tuple__0)arg);
                    newStmt.arg.Span = arg.Span;
                }
                else
                {
                    newStmt.arg = (P_Root.IArgType_NewStmt__1)arg;
                }
            }
            else
            {
                newStmt.arg = MkUserCnst(P_Root.UserCnstKind.NIL, span);
            }
            stmtStack.Push(newStmt);
        }

        private void PushNewExpr(string name, Span nameSpan, bool hasArgs, Span span)
        {
            Contract.Assert(!hasArgs || exprsStack.Count > 0);
            var newExpr = P_Root.MkNew();
            newExpr.name = MkString(name, nameSpan);
            newExpr.Span = span;
            newExpr.id = (P_Root.IArgType_New__2)MkId(span);
            if (hasArgs)
            {
                var arg = exprsStack.Pop();
                if (arg.Symbol == TheDefaultExprs.Symbol)
                {
                    newExpr.arg = P_Root.MkTuple((P_Root.IArgType_Tuple__0)arg);
                    newExpr.arg.Span = arg.Span;
                }
                else
                {
                    newExpr.arg = (P_Root.IArgType_New__1)arg;
                }
            }
            else
            {
                newExpr.arg = MkUserCnst(P_Root.UserCnstKind.NIL, span);
            }
            valueExprStack.Push(newExpr);
        }

        private void PushFunStmt(string name, bool hasArgs, Span span)
        {
            Contract.Assert(!hasArgs || exprsStack.Count > 0);
            var funStmt = P_Root.MkFunStmt();
            funStmt.name = MkString(name, span);
            funStmt.aout = MkUserCnst(P_Root.UserCnstKind.NIL, span);
            funStmt.Span = span;
            funStmt.label = P_Root.MkNumeric(GetNextTrampolineLabel());
            funStmt.id = (P_Root.IArgType_FunStmt__4) MkId(span);
            if (hasArgs)
            {
                funStmt.args = (P_Root.Exprs)exprsStack.Pop();
            }
            else
            {
                funStmt.args = MkUserCnst(P_Root.UserCnstKind.NIL, span);
            }
            stmtStack.Push(funStmt);
        }

        private void PushFunExpr(string name, bool hasArgs, Span span)
        {
            Contract.Assert(!hasArgs || exprsStack.Count > 0);
            var funExpr = P_Root.MkFunApp();
            funExpr.name = MkString(name, span);
            funExpr.Span = span;
            funExpr.id = (P_Root.IArgType_FunApp__2)MkId(span);
            if (hasArgs)
            {
                funExpr.args = (P_Root.Exprs)exprsStack.Pop();
            }
            else
            {
                funExpr.args = MkUserCnst(P_Root.UserCnstKind.NIL, span);
            }
            valueExprStack.Push(funExpr);
        }

        private void PushTupleExpr(bool isUnaryTuple)
        {
            Contract.Assert(valueExprStack.Count > 0);
            P_Root.Exprs fullExprs;
            var arg = (P_Root.IArgType_Exprs__1)valueExprStack.Pop();
            if (isUnaryTuple)
            {
                fullExprs = P_Root.MkExprs(P_Root.MkUserCnst(P_Root.UserCnstKind.NONE), arg, MkUserCnst(P_Root.UserCnstKind.NIL, arg.Span));
            }
            else
            {
                fullExprs = P_Root.MkExprs(P_Root.MkUserCnst(P_Root.UserCnstKind.NONE), arg, (P_Root.Exprs)exprsStack.Pop());
            }
            
            var tuple = P_Root.MkTuple(fullExprs);
            fullExprs.Span = arg.Span;
            tuple.Span = arg.Span;
            tuple.id = (P_Root.IArgType_Tuple__1)MkId(arg.Span);
            valueExprStack.Push(tuple);
        }

        private void PushNmdTupleExpr(string name, Span span, bool isUnaryTuple)
        {
            Contract.Assert(valueExprStack.Count > 0);
            P_Root.NamedExprs fullExprs;
            var arg = (P_Root.IArgType_NamedExprs__1)valueExprStack.Pop();
            if (isUnaryTuple)
            {
                fullExprs = P_Root.MkNamedExprs(
                    MkString(name, span),
                    arg, 
                    MkUserCnst(P_Root.UserCnstKind.NIL, arg.Span));
            }
            else
            {
                fullExprs = P_Root.MkNamedExprs(
                    MkString(name, span),                    
                    arg,
                    (P_Root.NamedExprs)exprsStack.Pop());
            }

            var tuple = P_Root.MkNamedTuple(fullExprs);
            fullExprs.Span = span;
            tuple.Span = span;
            tuple.id = (P_Root.IArgType_NamedTuple__1)MkId(span);
            valueExprStack.Push(tuple);
        }

        private void PushExprs()
        {
            Contract.Assert(exprsStack.Count > 0);
            Contract.Assert(valueExprStack.Count > 0);

            var oldExprs = exprsStack.Pop();
            if (oldExprs.Symbol != TheDefaultExprs.Symbol)
            {
                var coercedExprs = P_Root.MkExprs(
                    P_Root.MkUserCnst(P_Root.UserCnstKind.NONE),
                    (P_Root.IArgType_Exprs__1)oldExprs, 
                    MkUserCnst(P_Root.UserCnstKind.NIL, oldExprs.Span));
                coercedExprs.Span = oldExprs.Span;
                oldExprs = coercedExprs;
            }

            var arg = (P_Root.IArgType_Exprs__1)valueExprStack.Pop();
            var exprs = P_Root.MkExprs(qualifier.Pop(), arg, (P_Root.IArgType_Exprs__2)oldExprs);
            exprs.Span = arg.Span;
            exprsStack.Push(exprs);
        }

        private void PushNmdExprs(string name, Span span)
        {
            Contract.Assert(valueExprStack.Count > 0);
            Contract.Assert(exprsStack.Count > 0);
            var exprs = P_Root.MkNamedExprs(
                MkString(name, span),
                (P_Root.IArgType_NamedExprs__1)valueExprStack.Pop(),
                (P_Root.NamedExprs)exprsStack.Pop());
            exprs.Span = span;
            exprsStack.Push(exprs);
        }

        private void MoveValToNmdExprs(string name, Span span)
        {
            Contract.Assert(valueExprStack.Count > 0);
            var exprs = P_Root.MkNamedExprs(
                MkString(name, span),
                (P_Root.IArgType_NamedExprs__1)valueExprStack.Pop(),
                MkUserCnst(P_Root.UserCnstKind.NIL, span));
            exprs.Span = span;
            exprsStack.Push(exprs);
        }

        private void MoveValToExprs(bool makeIntoExprs)
        {
            Contract.Assert(valueExprStack.Count > 0);
            if (makeIntoExprs)
            {
                var arg = (P_Root.IArgType_Exprs__1)valueExprStack.Pop();
                var exprs = P_Root.MkExprs(qualifier.Pop(), arg, MkUserCnst(P_Root.UserCnstKind.NIL, arg.Span));
                exprs.Span = arg.Span;
                exprsStack.Push(exprs);
            }
            else
            {
                exprsStack.Push((P_Root.ExprsExt)valueExprStack.Pop());
            }
        }

        private void PushNulStmt(P_Root.UserCnstKind op, Span span)
        {
            var nulStmt = P_Root.MkNulStmt(MkUserCnst(op, span));
            nulStmt.Span = span;
            nulStmt.id = (P_Root.IArgType_NulStmt__1) MkId(span);
            stmtStack.Push(nulStmt);
        }

        private void PushSeq()
        {
            Contract.Assert(stmtStack.Count > 1);
            var seqStmt = P_Root.MkSeq();
            seqStmt.s2 = (P_Root.IArgType_Seq__1)stmtStack.Pop();
            seqStmt.s1 = (P_Root.IArgType_Seq__0)stmtStack.Pop();
            seqStmt.Span = seqStmt.s1.Span;
            stmtStack.Push(seqStmt);
        }

        private void PushNulExpr(P_Root.UserCnstKind op, Span span)
        {
            var nulExpr = P_Root.MkNulApp(MkUserCnst(op, span));
            nulExpr.Span = span;
            nulExpr.id = (P_Root.IArgType_NulApp__1)MkId(span);
            valueExprStack.Push(nulExpr);
        }

        private void PushUnExpr(P_Root.UserCnstKind op, Span span)
        {
            Contract.Assert(valueExprStack.Count > 0);
            var unExpr = P_Root.MkUnApp();
            unExpr.op = MkUserCnst(op, span);
            unExpr.arg1 = (P_Root.IArgType_UnApp__1)valueExprStack.Pop();
            unExpr.Span = span;
            unExpr.id = (P_Root.IArgType_UnApp__2)MkId(span);
            valueExprStack.Push(unExpr);
        }

        private void PushDefaultExpr(Span span)
        {
            Contract.Assert(typeExprStack.Count > 0);
            var defExpr = P_Root.MkDefault();
            defExpr.type = (P_Root.IArgType_Default__0)typeExprStack.Pop();
            defExpr.Span = span;
            defExpr.id = (P_Root.IArgType_Default__1)MkId(span);
            valueExprStack.Push(defExpr);
        }

        private void PushIntExpr(string intStr, Span span)
        {
            int val;
            if (!int.TryParse(intStr, out val))
            {
                var errFlag = new Flag(
                                 SeverityKind.Error,
                                 span,
                                 Constants.BadSyntax.ToString(string.Format("Bad int constant {0}", intStr)),
                                 Constants.BadSyntax.Code,
                                 parseSource);
                parseFailed = true;
                parseFlags.Add(errFlag);
            }

            var nulExpr = P_Root.MkNulApp(MkNumeric(val, span));
            nulExpr.Span = span;
            nulExpr.id = (P_Root.IArgType_NulApp__1)MkId(span);
            valueExprStack.Push(nulExpr);
        }

        private void PushCast(Span span)
        {
            Contract.Assert(valueExprStack.Count > 0);
            Contract.Assert(typeExprStack.Count > 0);
            var cast = P_Root.MkCast(
                (P_Root.IArgType_Cast__0)valueExprStack.Pop(),
                (P_Root.IArgType_Cast__1)typeExprStack.Pop());
            cast.Span = span;
            cast.id = (P_Root.IArgType_Cast__2)MkId(span);
            valueExprStack.Push(cast);
        }

        private void PushIte(bool hasElse, Span span)
        {
            Contract.Assert(valueExprStack.Count > 0);
            var iteStmt = P_Root.MkIte();
            iteStmt.Span = span;
            iteStmt.id = (P_Root.IArgType_Ite__3) MkId(span);
            iteStmt.cond = (P_Root.IArgType_Ite__0)valueExprStack.Pop();
            if (hasElse)
            {
                Contract.Assert(stmtStack.Count > 1);
                iteStmt.@false = (P_Root.IArgType_Ite__2)stmtStack.Pop();
                iteStmt.@true = (P_Root.IArgType_Ite__1)stmtStack.Pop();
            }
            else
            {
                Contract.Assert(stmtStack.Count > 0);
                var skipStmt = P_Root.MkNulStmt(MkUserCnst(P_Root.UserCnstKind.SKIP, span));
                skipStmt.Span = span;
                skipStmt.id = (P_Root.IArgType_NulStmt__1) MkId(span);
                iteStmt.@true = (P_Root.IArgType_Ite__1)stmtStack.Pop();
                iteStmt.@false = skipStmt;
            }
            stmtStack.Push(iteStmt);
        }

        private void PushReturn(bool returnsValue, Span span)
        {
            Contract.Assert(!returnsValue || valueExprStack.Count > 0);
            var retStmt = P_Root.MkReturn();
            retStmt.Span = span;
            if (returnsValue)
            {
                retStmt.expr = (P_Root.IArgType_Return__0)valueExprStack.Pop();
            }
            else
            {
                retStmt.expr = MkUserCnst(P_Root.UserCnstKind.NIL, span);
            }
            retStmt.id = (P_Root.IArgType_Return__1) MkId(span);
            stmtStack.Push(retStmt);
        }

        private void PushWhile(Span span)
        {
            Contract.Assert(valueExprStack.Count > 0 && stmtStack.Count > 0);
            var whileStmt = P_Root.MkWhile(
                (P_Root.IArgType_While__0)valueExprStack.Pop(),
                (P_Root.IArgType_While__1)stmtStack.Pop());
            whileStmt.Span = span;
            whileStmt.id = (P_Root.IArgType_While__2) MkId(span);
            stmtStack.Push(whileStmt);
        }

        private void PushAssert(Span span)
        {
            Contract.Assert(valueExprStack.Count > 0);
            var assertStmt = P_Root.MkAssert();
            assertStmt.arg = (P_Root.IArgType_Assert__0)valueExprStack.Pop();
            assertStmt.msg = MkUserCnst(P_Root.UserCnstKind.NIL, span);
            assertStmt.Span = span;
            assertStmt.id = (P_Root.IArgType_Assert__2) MkId(span);
            stmtStack.Push(assertStmt);
        }
        
        private void PushAssert(string msg, Span msgSpan, Span span)
        {
            Contract.Assert(valueExprStack.Count > 0);
            var assertStmt = P_Root.MkAssert();
            assertStmt.arg = (P_Root.IArgType_Assert__0)valueExprStack.Pop();
            assertStmt.msg = MkString(msg, msgSpan);
            assertStmt.Span = span;
            assertStmt.id = (P_Root.IArgType_Assert__2) MkId(span);
            stmtStack.Push(assertStmt);
        }

        private void PushPrint(string msg, Span msgSpan, Span span, bool hasArgs)
        {
            P_Root.IArgType_Print__2 args = P_Root.MkUserCnst(P_Root.UserCnstKind.NIL);
            int numArgs = 0;
            if (hasArgs)
            {
                args = (P_Root.IArgType_Print__2)exprsStack.Pop();
                P_Root.Exprs iter = args as P_Root.Exprs;
                while (iter != null)
                {
                    numArgs++;
                    iter = iter.tail as P_Root.Exprs;
                }
            }
            List<string> segments;
            List<int> formatArgs;
            if (ParseFormatString(msg, numArgs, msgSpan, out segments, out formatArgs))
            {
                var printStmt = P_Root.MkPrint();
                printStmt.msg = MkString(segments[0], msgSpan);
                P_Root.IArgType_Print__1 segs = P_Root.MkUserCnst(P_Root.UserCnstKind.NIL);
                for (int i = formatArgs.Count-1; i >= 0; i--)
                {
                    var seg = P_Root.MkSegments();
                    seg.formatArg = MkNumeric(formatArgs[i], msgSpan);
                    seg.str = MkString(segments[i + 1], msgSpan);
                    seg.tl = (P_Root.IArgType_Segments__2)segs;
                    segs = seg;
                }
                printStmt.segs = segs;
                printStmt.args = args;
                printStmt.id = (P_Root.IArgType_Print__3) MkId(span);
                printStmt.Span = span;
                stmtStack.Push(printStmt);
            }
            else
            {
                var skipStmt = P_Root.MkNulStmt(MkUserCnst(P_Root.UserCnstKind.SKIP, span));
                skipStmt.id = (P_Root.IArgType_NulStmt__1) MkId(span);
                stmtStack.Push(skipStmt);
            }
        }

        private void PushBinStmt(P_Root.UserCnstKind op, Span span)
        {
            Contract.Assert(valueExprStack.Count > 1);
            P_Root.Expr arg2 = valueExprStack.Pop();
            P_Root.Expr arg1 = valueExprStack.Pop();
            if (op == P_Root.UserCnstKind.ASSIGN && arg1 is P_Root.Name && arg2 is P_Root.FunApp)
            {
                P_Root.Name aout = arg1 as P_Root.Name;
                P_Root.FunApp funCall = arg2 as P_Root.FunApp;
                var funStmt = P_Root.MkFunStmt();
                funStmt.name = (P_Root.IArgType_FunStmt__0)funCall.name;
                funStmt.args = (P_Root.IArgType_FunStmt__1)funCall.args;
                funStmt.aout = (P_Root.IArgType_FunStmt__2)aout;
                funStmt.label = MkNumeric(GetNextTrampolineLabel(), span);
                funStmt.Span = span;
                funStmt.id = (P_Root.IArgType_FunStmt__4) MkId(span);
                stmtStack.Push(funStmt);
            }
            else
            {
                var binStmt = P_Root.MkBinStmt();
                binStmt.op = MkUserCnst(op, span);
                binStmt.arg2 = (P_Root.IArgType_BinStmt__2)arg2;
                binStmt.arg1 = (P_Root.IArgType_BinStmt__1)arg1;
                binStmt.Span = span;
                binStmt.id = (P_Root.IArgType_BinStmt__3) MkId(span);
                stmtStack.Push(binStmt);
            }
        }

        private void PushBinExpr(P_Root.UserCnstKind op, Span span)
        {
            Contract.Assert(valueExprStack.Count > 1);
            var binApp = P_Root.MkBinApp();
            binApp.op = MkUserCnst(op, span);
            binApp.arg2 = (P_Root.IArgType_BinApp__2)valueExprStack.Pop();
            binApp.arg1 = (P_Root.IArgType_BinApp__1)valueExprStack.Pop();
            binApp.Span = span;
            binApp.id = (P_Root.IArgType_BinApp__3)MkId(span);
            valueExprStack.Push(binApp);
        }

        private void PushName(string name, Span span)
        {
            var nameNode = P_Root.MkName(MkString(name, span));
            nameNode.Span = span;
            nameNode.id = (P_Root.IArgType_Name__1)MkId(span);
            valueExprStack.Push(nameNode);
        }

        private void PushField(string name, Span span)
        {
            Contract.Assert(valueExprStack.Count > 0);
            var field = P_Root.MkField();
            field.name = MkString(name, span);
            field.arg = (P_Root.IArgType_Field__0)valueExprStack.Pop();
            field.Span = span;
            field.id = (P_Root.IArgType_Field__2)MkId(span);
            valueExprStack.Push(field);
        }

        private void PushFieldInt(string indexStr, Span span)
        {
            Contract.Assert(valueExprStack.Count > 0);
            int index = 0;
            if (!int.TryParse(indexStr, out index) || index < 0)
            {
                var errFlag = new Flag(
                                 SeverityKind.Error,
                                 span,
                                 Constants.BadSyntax.ToString(string.Format("Bad tuple index {0}", indexStr)),
                                 Constants.BadSyntax.Code,
                                 parseSource);
                parseFailed = true;
                parseFlags.Add(errFlag);
                index = 0;
            }

            var field = P_Root.MkField();
            field.name = MkNumeric(index, span);
            field.arg = (P_Root.IArgType_Field__0)valueExprStack.Pop();
            field.Span = span;
            field.id = (P_Root.IArgType_Field__2)MkId(span);
            valueExprStack.Push(field);
        }

        private void PushGroup(string name, Span nameSpan, Span span)
        {
            var groupName = P_Root.MkQualifiedName(MkString(name, nameSpan));
            groupName.Span = span;
            if (groupStack.Count == 0)
            {
                groupName.qualifier = MkUserCnst(P_Root.UserCnstKind.NIL, span);
            }
            else
            {
                groupName.qualifier = groupStack.Peek();
            }

            groupStack.Push(groupName);
        }

        private void QualifyStateTarget(string name, Span span)
        {
            if (crntStateTargetName == null)
            {
                crntStateTargetName = P_Root.MkQualifiedName(
                    MkString(name, span),
                    MkUserCnst(P_Root.UserCnstKind.NIL, span));
            }
            else
            {
                crntStateTargetName = P_Root.MkQualifiedName(
                    MkString(name, span),
                    crntStateTargetName);
            }
            crntStateTargetName.Span = span;
        }

        private void QualifyGotoTarget(string name, Span span)
        {
            if (crntGotoTargetName == null)
            {
                crntGotoTargetName = P_Root.MkQualifiedName(
                    MkString(name, span),
                    MkUserCnst(P_Root.UserCnstKind.NIL, span));
            }
            else
            {
                crntGotoTargetName = P_Root.MkQualifiedName(
                    MkString(name, span),
                    crntGotoTargetName);
            }
            crntGotoTargetName.Span = span;
        }

        #endregion

        #region Node setters
        private void SetEventCard(string cardStr, bool isAssert, Span span)
        {
            var evDecl = GetCurrentEventDecl(span);
            int card;
            if (!int.TryParse(cardStr, out card) || card < 0)
            {
                var errFlag = new Flag(
                                 SeverityKind.Error,
                                 span,
                                 Constants.BadSyntax.ToString(string.Format("Bad event cardinality {0}", cardStr)),
                                 Constants.BadSyntax.Code,
                                 parseSource);
                parseFailed = true;
                parseFlags.Add(errFlag);
                return;
            }

            if (isAssert)
            {
                var assertNode = P_Root.MkAssertMaxInstances(MkNumeric(card, span));
                assertNode.Span = span;
                evDecl.card = assertNode;
            }
            else
            {
                var assumeNode = P_Root.MkAssumeMaxInstances(MkNumeric(card, span));
                assumeNode.Span = span;
                evDecl.card = assumeNode;
            }
        }

        private void SetMachineCard(string cardStr, bool isAssert, Span span)
        {
            var machDecl = GetCurrentMachineDecl(span);
            int card;
            if (!int.TryParse(cardStr, out card) || card < 0)
            {
                var errFlag = new Flag(
                                 SeverityKind.Error,
                                 span,
                                 Constants.BadSyntax.ToString(string.Format("Bad machine cardinality {0}", cardStr)),
                                 Constants.BadSyntax.Code,
                                 parseSource);
                parseFailed = true;
                parseFlags.Add(errFlag);
                return;
            }

            if (isAssert)
            {
                var assertNode = P_Root.MkAssertMaxInstances(MkNumeric(card, span));
                assertNode.Span = span;
                machDecl.card = assertNode;
            }
            else
            {
                var assumeNode = P_Root.MkAssumeMaxInstances(MkNumeric(card, span));
                assumeNode.Span = span;
                machDecl.card = assumeNode;
            }
        }

        private void SetStateIsHot(Span span)
        {
            var state = GetCurrentStateDecl(span);
            state.temperature = MkUserCnst(P_Root.UserCnstKind.HOT, span);
        }

        private void SetStateIsCold(Span span)
        {
            var state = GetCurrentStateDecl(span);
            state.temperature = MkUserCnst(P_Root.UserCnstKind.COLD, span);
        }

        private void SetTrigAnnotated(Span span)
        {
            crntAnnotSpan = span;
            isTrigAnnotated = true;
        }

        private void SetStateEntry(Span entrySpan, Span exitSpan)
        {
            Contract.Assert(stmtStack.Count > 0);
            P_Root.StateDecl state;
            var stmt = (P_Root.IArgType_AnonFunDecl__3)stmtStack.Pop();
            state = GetCurrentStateDecl(stmt.Span);
            var entry = P_Root.MkAnonFunDecl((P_Root.MachineDecl)state.owner, P_Root.MkUserCnst(P_Root.UserCnstKind.NIL), (P_Root.IArgType_AnonFunDecl__2)localVarStack.LocalVarDecl, stmt, (P_Root.IArgType_AnonFunDecl__4)localVarStack.ContextLocalVarDecl);
            entry.Span = stmt.Span;
            entry.id = (P_Root.IArgType_AnonFunDecl__5) MkId(entrySpan, exitSpan);
            parseProgram.AnonFunctions.Add(entry);
            localVarStack = new LocalVarStack(this);

            if (IsSkipFun((P_Root.GroundTerm)state.entryAction))
            {
                state.entryAction = (P_Root.IArgType_StateDecl__2)entry;
            }
            else
            {
                var errFlag = new Flag(
                                 SeverityKind.Error,
                                 entry.Span,
                                 Constants.BadSyntax.ToString("Too many entry functions"),
                                 Constants.BadSyntax.Code,
                                 parseSource);
                parseFailed = true;
                parseFlags.Add(errFlag);
            }
        }

        private void SetStateEntry(string actionName, Span actionSpan)
        {
            P_Root.IArgType_StateDecl__2 entry;
            P_Root.StateDecl state;
            entry = (P_Root.IArgType_StateDecl__2)MkString(actionName, actionSpan);
            state = GetCurrentStateDecl(actionSpan);

            if (IsSkipFun((P_Root.GroundTerm)state.entryAction))
            {
                state.entryAction = (P_Root.IArgType_StateDecl__2)entry;
            }
            else
            {
                var errFlag = new Flag(
                                 SeverityKind.Error,
                                 entry.Span,
                                 Constants.BadSyntax.ToString("Too many entry functions"),
                                 Constants.BadSyntax.Code,
                                 parseSource);
                parseFailed = true;
                parseFlags.Add(errFlag);
            }
        }

        private void SetStateExit(Span entrySpan, Span exitSpan)
        {
            Contract.Assert(stmtStack.Count > 0);
            P_Root.StateDecl state;

            var stmt = (P_Root.IArgType_AnonFunDecl__3)stmtStack.Pop();
            state = GetCurrentStateDecl(stmt.Span);
            var exit = P_Root.MkAnonFunDecl((P_Root.MachineDecl)state.owner, P_Root.MkUserCnst(P_Root.UserCnstKind.NIL), (P_Root.IArgType_AnonFunDecl__2)localVarStack.LocalVarDecl, stmt, (P_Root.IArgType_AnonFunDecl__4)localVarStack.ContextLocalVarDecl);
            exit.Span = stmt.Span;
            exit.id = (P_Root.IArgType_AnonFunDecl__5)MkId(entrySpan, exitSpan);
            parseProgram.AnonFunctions.Add(exit);
            localVarStack = new LocalVarStack(this);

            if (IsSkipFun((P_Root.GroundTerm)state.exitFun))
            {
                state.exitFun = (P_Root.IArgType_StateDecl__3)exit;
            }
            else
            {
                var errFlag = new Flag(
                                 SeverityKind.Error,
                                 exit.Span,
                                 Constants.BadSyntax.ToString("Too many exit functions"),
                                 Constants.BadSyntax.Code,
                                 parseSource);
                parseFailed = true;
                parseFlags.Add(errFlag);
            }
        }

        private void SetStateExit(string funtionName, Span functionSpan)
        {
            P_Root.IArgType_StateDecl__3 exit;
            P_Root.StateDecl state;
            exit = (P_Root.IArgType_StateDecl__3)MkString(funtionName, functionSpan);
            state = GetCurrentStateDecl(functionSpan);

            if (IsSkipFun((P_Root.GroundTerm)state.exitFun))
            {
                state.exitFun = (P_Root.IArgType_StateDecl__3)exit;
            }
            else
            {
                var errFlag = new Flag(
                                 SeverityKind.Error,
                                 exit.Span,
                                 Constants.BadSyntax.ToString("Too many exit functions"),
                                 Constants.BadSyntax.Code,
                                 parseSource);
                parseFailed = true;
                parseFlags.Add(errFlag);
            }
        }

        private void SetEventType(Span span)
        {
            var evDecl = GetCurrentEventDecl(span);
            Contract.Assert(typeExprStack.Count > 0);
            evDecl.type = (P_Root.IArgType_EventDecl__2)typeExprStack.Pop();
        }

        private void SetInterfaceConstType(Span span)
        {
            var inDecl = GetCurrentInterfaceTypeDecl(span);
            Contract.Assert(typeExprStack.Count > 0);
            inDecl.argType = (P_Root.IArgType_InterfaceTypeDecl__2)typeExprStack.Pop();
        }

        private void SetMachineProtoConstType(Span span)
        {
            var machineProto = GetCurrentMachineProtoDecl(span);
            Contract.Assert(typeExprStack.Count > 0);
            machineProto.constType = (P_Root.IArgType_MachineProtoDecl__1)typeExprStack.Pop();
        }

        private void SetFunKind(P_Root.UserCnstKind kind, Span span)
        {
            if (Options.test) return;
            var funDecl = GetCurrentFunDecl(span);
            funDecl.kind = MkUserCnst(kind, span);
        }

        private void SetFunName(string name, Span span)
        {
            if(isFunProtoDecl)
            {
                var funProtoDecl = GetCurrentFunProtoDecl(span);
                funProtoDecl.name = MkString(name, span);
                if(IsValidName(TopDecl.FunProto, name, span))
                {
                    topDeclNames.funProto.Add(name);
                }
            }
            else
            {
                var funDecl = GetCurrentFunDecl(span);
                funDecl.name = MkString(name, span);
            }
            
            //catch early errors
            if (crntFunNames.Contains(name))
            {
                var errFlag = new Flag(
                                     SeverityKind.Error,
                                     span,
                                     Constants.BadSyntax.ToString(string.Format("A function with name {0} already declared", name)),
                                     Constants.BadSyntax.Code,
                                     parseSource);
                parseFailed = true;
                parseFlags.Add(errFlag);
            }
            else
            {
                crntFunNames.Add(name);
            }
        }

        private void SetFunParams(Span span)
        {
            if(isFunProtoDecl)
            {
                Contract.Assert(typeExprStack.Count > 0);
                var funDecl = GetCurrentFunProtoDecl(span);
                funDecl.@params = (P_Root.IArgType_FunProtoDecl__1)typeExprStack.Pop();
            }
            else
            {
                Contract.Assert(typeExprStack.Count > 0);
                var funDecl = GetCurrentFunDecl(span);
                funDecl.@params = (P_Root.IArgType_FunDecl__3)typeExprStack.Pop();
                localVarStack = new LocalVarStack(this, (P_Root.IArgType_NmdTupType__1)funDecl.@params);
            }
            
        }

        private void SetFunReturn(Span span)
        {
            if(isFunProtoDecl)
            {
                Contract.Assert(typeExprStack.Count > 0);
                var funDecl = GetCurrentFunProtoDecl(span);
                funDecl.@return = (P_Root.IArgType_FunProtoDecl__2)typeExprStack.Pop();
            }
            else
            {
                Contract.Assert(typeExprStack.Count > 0);
                var funDecl = GetCurrentFunDecl(span);
                funDecl.@return = (P_Root.IArgType_FunDecl__4)typeExprStack.Pop();
            }
            
        }
        #endregion

        #region Adders
        private void AddModelTypeDef(string name, Span nameSpan, Span typeDefSpan)
        {
            AddTypeDef(name, nameSpan, typeDefSpan);
            if (!Options.test)
            {
                var modelType = P_Root.MkModelType(MkString(name, nameSpan));
                parseProgram.ModelTypes.Add(modelType);
            }
        }

        private void AddTypeDef(string name, Span nameSpan, Span typeDefSpan)
        {
            if (IsValidName(TopDecl.TypeDef, name, nameSpan))
            {
                topDeclNames.typeNames.Add(name);
            }
            var type = (P_Root.IArgType_TypeDef__1)typeExprStack.Pop();
            var typeDef = P_Root.MkTypeDef(MkString(name, nameSpan), type);
            typeDef.Span = typeDefSpan;
            typeDef.id = (P_Root.IArgType_TypeDef__2)MkId(typeDefSpan);
            parseProgram.TypeDefs.Add(typeDef);
        }

        P_Root.IArgType_StringList__1 enumElemList = P_Root.MkUserCnst(P_Root.UserCnstKind.NIL);
        P_Root.IArgType_IntegerList__1 enumElemValList = P_Root.MkUserCnst(P_Root.UserCnstKind.NIL);

        void AddEnumElem(string name, Span nameSpan)
        {
            enumElemList = P_Root.MkStringList(MkString(name, nameSpan), enumElemList);
        }

        void AddEnumElem(string name, Span nameSpan, string intStr, Span intStrSpan)
        {
            int val;
            if (int.TryParse(intStr, out val))
            {
                enumElemList = P_Root.MkStringList(MkString(name, nameSpan), enumElemList);
                enumElemValList = P_Root.MkIntegerList(MkNumeric(val, intStrSpan), enumElemValList);
            }
            else
            {
                var errFlag = new Flag(
                     SeverityKind.Error,
                     intStrSpan,
                     Constants.BadSyntax.ToString(string.Format("Bad int constant {0}", intStr)),
                     Constants.BadSyntax.Code,
                     parseSource);
                parseFailed = true;
                parseFlags.Add(errFlag);
            }
        }

        void AddEnumTypeDef(string name, Span nameSpan, Span enumTypeDefSpan)
        {
            P_Root.EnumTypeDef enumTypeDef = P_Root.MkEnumTypeDef(MkString(name, nameSpan), (P_Root.StringList)enumElemList, (P_Root.IArgType_EnumTypeDef__2)enumElemValList);
            enumElemList = P_Root.MkUserCnst(P_Root.UserCnstKind.NIL);
            enumElemValList = P_Root.MkUserCnst(P_Root.UserCnstKind.NIL);
            enumTypeDef.Span = enumTypeDefSpan;
            enumTypeDef.id = (P_Root.IArgType_EnumTypeDef__3)MkId(enumTypeDefSpan);

            if (IsValidName(TopDecl.Enum, name, nameSpan))
            {
                topDeclNames.enumNames.Add(name);
            }
            parseProgram.EnumTypeDefs.Add(enumTypeDef);
        }

        private void AddGroup()
        {
            groupStack.Pop();
        }

        private void AddEventSet(string name, Span nameSpan, Span span)
        {
            if (IsValidName(TopDecl.EventSet, name, nameSpan))
                topDeclNames.eventSetNames.Add(name);

            var eventset = new P_Root.EventSetDecl();
            eventset.name = MkString(name, nameSpan);
            eventset.id = (P_Root.IArgType_EventSetDecl__1)MkId(nameSpan);
            eventset.Span = span;
            parseProgram.EventSetDecl.Add(eventset);

            foreach (var ev in crntEventList)
            {
                var eventsetContains = new P_Root.EventSetContains();
                eventsetContains.evset = eventset;
                eventsetContains.ev = (P_Root.IArgType_EventSetContains__1)ev;
                parseProgram.EventSetContains.Add(eventsetContains);
            }
            crntEventList.Clear();
        }

        private void AddInterfaceType(string iname, string esname, Span inameSpan, Span iesnameSpan, Span span)
        {
            var inDecl = GetCurrentInterfaceTypeDecl(span);
            inDecl.Span = span;
            inDecl.name = MkString(iname, inameSpan);
            inDecl.id = (P_Root.IArgType_InterfaceTypeDecl__3)MkId(inameSpan);
            if(esname == null)
            {
                //declaration contains set of events
                Contract.Assert(crntEventList.Count() > 0);
                var anonEventSetName = "__AnonEventSet_" + anonEventSetCounter;
                anonEventSetCounter++;
                var eventset = new P_Root.EventSetDecl();
                eventset.name = MkString(anonEventSetName, iesnameSpan);
                eventset.id = (P_Root.IArgType_EventSetDecl__1)MkId(inameSpan);
                eventset.Span = span;
                parseProgram.EventSetDecl.Add(eventset);
                foreach (var ev in crntEventList)
                {
                    var eventsetContains = new P_Root.EventSetContains();
                    eventsetContains.evset = eventset;
                    eventsetContains.ev = (P_Root.IArgType_EventSetContains__1)ev;
                    eventsetContains.Span = ev.Span;
                    parseProgram.EventSetContains.Add(eventsetContains);
                }
                inDecl.evsetName = MkString(anonEventSetName, iesnameSpan);
                crntEventList.Clear();
            }
            else
            {
                inDecl.evsetName = MkString(esname, iesnameSpan);
            }
            
            parseProgram.InterfaceTypeDecl.Add(inDecl);
            if (IsValidName(TopDecl.Interface, iname, inameSpan))
            {
                topDeclNames.interfaceNames.Add(iname);
            }
            crntInterfaceDecl = null;
        }

        private void AddVarDecl(string name, Span span)
        {
            var varDecl = P_Root.MkVarDecl();
            varDecl.name = MkString(name, span);
            varDecl.owner = GetCurrentMachineDecl(span);
            varDecl.Span = span;
            varDecl.id = (P_Root.IArgType_VarDecl__3)MkId(span);
            crntVarList.Add(varDecl);
            if (crntVarNames.Contains(name))
            {
                var errFlag = new Flag(
                                     SeverityKind.Error,
                                     span,
                                     Constants.BadSyntax.ToString(string.Format("A variable with name {0} already declared", name)),
                                     Constants.BadSyntax.Code,
                                     parseSource);
                parseFailed = true;
                parseFlags.Add(errFlag);
            }
            else
            {
                crntVarNames.Add(name);
            }
        }

        private void AddToEventList(string name, Span span)
        {
            if (crntEventList.Where(e => ((string)e.Symbol == name)).Count() >= 1)
            {
                var errFlag = new Flag(
                                     SeverityKind.Error,
                                     span,
                                     Constants.BadSyntax.ToString(string.Format("Event {0} listed multiple times in the event list", name)),
                                     Constants.BadSyntax.Code,
                                     parseSource);
                parseFailed = true;
                parseFlags.Add(errFlag);
            }
            else
            {
                crntEventList.Add(MkString(name, span));
            }
        }

        private void AddToEventList(P_Root.UserCnstKind kind, Span span)
        {
            crntEventList.Add(MkUserCnst(kind, span));
        }

        private void AddDefersOrIgnores(bool isDefer, Span span)
        {
            Contract.Assert(crntEventList.Count > 0);
            Contract.Assert(!isTrigAnnotated || crntAnnotStack.Count > 0);
            var state = GetCurrentStateDecl(span);
            var kind = MkUserCnst(isDefer ? P_Root.UserCnstKind.DEFER : P_Root.UserCnstKind.IGNORE, span);
            var annots = isTrigAnnotated ? crntAnnotStack.Pop() : null;
            foreach (var e in crntEventList)
            {
                var defOrIgn = P_Root.MkDoDecl(state, (P_Root.IArgType_DoDecl__1)e, kind);
                defOrIgn.Span = span;
                defOrIgn.id = (P_Root.IArgType_DoDecl__3)MkId(span);
                parseProgram.Dos.Add(defOrIgn);
                if (isTrigAnnotated)
                {
                    foreach (var kv in annots)
                    {
                        var annot = P_Root.MkAnnotation(
                            defOrIgn,
                            kv.Item1,
                            (P_Root.IArgType_Annotation__2)kv.Item2);
                        annot.Span = crntAnnotSpan;
                        annot.id = (P_Root.IArgType_Annotation__3)MkId(crntAnnotSpan);
                        parseProgram.Annotations.Add(annot);
                    }
                }
            }

            isTrigAnnotated = false;
            crntEventList.Clear();
        }

        private void AddTransitionWithAction(Span entrySpan, Span exitSpan, Span span)
        {
            Contract.Assert(stmtStack.Count > 0);

            var state = GetCurrentStateDecl(span);
            var stmt = (P_Root.IArgType_AnonFunDecl__3)stmtStack.Pop();
            var action = P_Root.MkAnonFunDecl((P_Root.MachineDecl)state.owner, P_Root.MkUserCnst(P_Root.UserCnstKind.NIL), (P_Root.IArgType_AnonFunDecl__2)localVarStack.LocalVarDecl, stmt, (P_Root.IArgType_AnonFunDecl__4)localVarStack.ContextLocalVarDecl);
            action.Span = stmt.Span;
            action.id = (P_Root.IArgType_AnonFunDecl__5)MkId(entrySpan, exitSpan);
            parseProgram.AnonFunctions.Add(action);
            localVarStack = new LocalVarStack(this);
            AddTransitionHelper(state, action, span);
        }

        private void AddTransitionWithAction(string actName, Span actNameSpan, Span span)
        {
            var state = GetCurrentStateDecl(span);
            P_Root.IArgType_TransDecl__3 action;
            action = MkString(actName, actNameSpan);
            AddTransitionHelper(state, action, span);
        }

        private void AddTransitionHelper(P_Root.StateDecl state, P_Root.IArgType_TransDecl__3 action, Span span)
        {
            Contract.Assert(onEventList.Count > 0);
            Contract.Assert(crntStateTargetName != null);
            Contract.Assert(!isTrigAnnotated || crntAnnotStack.Count > 0);
            var annots = isTrigAnnotated ? crntAnnotStack.Pop() : null;
            foreach (var e in onEventList)
            {
                var trans = P_Root.MkTransDecl(state, (P_Root.IArgType_TransDecl__1)e, crntStateTargetName, action);
                trans.Span = span;
                trans.id = (P_Root.IArgType_TransDecl__4)MkId(span);
                parseProgram.Transitions.Add(trans);
                if (isTrigAnnotated)
                {
                    foreach (var kv in annots)
                    {
                        var annot = P_Root.MkAnnotation(
                            trans,
                            kv.Item1,
                            (P_Root.IArgType_Annotation__2)kv.Item2);
                        annot.Span = crntAnnotSpan;
                        annot.id = (P_Root.IArgType_Annotation__3)MkId(crntAnnotSpan);
                        parseProgram.Annotations.Add(annot);
                    }
                }
            }

            isTrigAnnotated = false;
            crntStateTargetName = null;
            onEventList.Clear();
        }

        private void AddTransition(bool isPush, Span span)
        {
            Contract.Assert(onEventList.Count > 0);
            Contract.Assert(crntStateTargetName != null);
            Contract.Assert(!isTrigAnnotated || crntAnnotStack.Count > 0);

            var annots = isTrigAnnotated ? crntAnnotStack.Pop() : null;
            var state = GetCurrentStateDecl(span);
            P_Root.IArgType_TransDecl__3 action;
            if (isPush)
            {
                action = MkUserCnst(P_Root.UserCnstKind.PUSH, span);
            }
            else
            {
                action = MkSkipFun((P_Root.MachineDecl)state.owner, span);
            }

            foreach (var e in onEventList)
            {
                var trans = P_Root.MkTransDecl(state, (P_Root.IArgType_TransDecl__1)e, crntStateTargetName, action);
                trans.Span = span;
                trans.id = (P_Root.IArgType_TransDecl__4)MkId(span);
                parseProgram.Transitions.Add(trans);
                if (isTrigAnnotated)
                {
                    foreach (var kv in annots)
                    {
                        var annot = P_Root.MkAnnotation(
                            trans,
                            kv.Item1,
                            (P_Root.IArgType_Annotation__2)kv.Item2);
                        annot.Span = crntAnnotSpan;
                        annot.id = (P_Root.IArgType_Annotation__3)MkId(crntAnnotSpan);
                        parseProgram.Annotations.Add(annot);
                    }
                }
            }

            isTrigAnnotated = false;
            crntStateTargetName = null;
            onEventList.Clear();
        }

        private void AddProgramAnnots(Span span)
        {
            Contract.Assert(crntAnnotStack.Count > 0);
            var annots = crntAnnotStack.Pop();
            foreach (var kv in annots)
            {
                var annot = P_Root.MkAnnotation(
                    MkUserCnst(P_Root.UserCnstKind.NIL, span),
                    kv.Item1,
                    (P_Root.IArgType_Annotation__2)kv.Item2);
                annot.Span = span;
                annot.id = (P_Root.IArgType_Annotation__3)MkId(span);
                parseProgram.Annotations.Add(annot);  
            }
        }

        private void AddAnnotStringVal(string keyName, string valStr, Span keySpan, Span valSpan)
        {
            crntAnnotList.Add(
                new Tuple<P_Root.StringCnst, P_Root.AnnotValue>(
                    MkString(keyName, keySpan),
                    MkString(valStr, valSpan)));
        }

        private void AddAnnotIntVal(string keyName, string intStr, Span keySpan, Span valSpan)
        {
            int val;
            if (!int.TryParse(intStr, out val))
            {
                var errFlag = new Flag(
                                 SeverityKind.Error,
                                 valSpan,
                                 Constants.BadSyntax.ToString(string.Format("Bad int constant {0}", intStr)),
                                 Constants.BadSyntax.Code,
                                 parseSource);
                parseFailed = true;
                parseFlags.Add(errFlag);
                return;
            }

            crntAnnotList.Add(
                new Tuple<P_Root.StringCnst, P_Root.AnnotValue>(
                    MkString(keyName, keySpan),
                    MkNumeric(val, valSpan)));
        }

        private void AddAnnotUsrCnstVal(string keyName, P_Root.UserCnstKind valKind, Span keySpan, Span valSpan)
        {
            crntAnnotList.Add(
                new Tuple<P_Root.StringCnst, P_Root.AnnotValue>(
                    MkString(keyName, keySpan),
                    MkUserCnst(valKind, valSpan)));
        }

        private void AddCaseAnonyAction(Span caseSpan, Span entrySpan, Span exitSpan)
        {
            var stmt = (P_Root.IArgType_AnonFunDecl__3)stmtStack.Pop();
            P_Root.IArgType_AnonFunDecl__0 owner =
                    crntMachDecl == null
                    ? (P_Root.IArgType_AnonFunDecl__0)P_Root.MkUserCnst(P_Root.UserCnstKind.NIL)
                    : (P_Root.IArgType_AnonFunDecl__0)crntMachDecl;
            P_Root.IArgType_AnonFunDecl__1 ownerFun =
                    crntFunDecl == null
                    ? (P_Root.IArgType_AnonFunDecl__1)P_Root.MkUserCnst(P_Root.UserCnstKind.NIL)
                    : (P_Root.IArgType_AnonFunDecl__1)crntFunDecl.name;
            var anonAction = P_Root.MkAnonFunDecl(owner, ownerFun, (P_Root.IArgType_AnonFunDecl__2)localVarStack.LocalVarDecl, stmt, (P_Root.IArgType_AnonFunDecl__4)localVarStack.ContextLocalVarDecl);
            anonAction.Span = stmt.Span;
            anonAction.id = (P_Root.IArgType_AnonFunDecl__5)MkId(entrySpan, exitSpan);
            parseProgram.AnonFunctions.Add(anonAction);
            var caseEventList = localVarStack.Pop();
            foreach (var e in caseEventList)
            {
                localVarStack.AddCase((P_Root.IArgType_Cases__0)e, anonAction, caseSpan);
            }
        }

        private void AddDoAnonyAction(Span entrySpan, Span exitSpan, Span span)
        {
            Contract.Assert(onEventList.Count > 0);
            Contract.Assert(!isTrigAnnotated || crntAnnotStack.Count > 0);

            var state = GetCurrentStateDecl(new Span());
            var stmt = (P_Root.IArgType_AnonFunDecl__3)stmtStack.Pop();
            var annots = isTrigAnnotated ? crntAnnotStack.Pop() : null;
            var anonAction = P_Root.MkAnonFunDecl((P_Root.MachineDecl)state.owner, P_Root.MkUserCnst(P_Root.UserCnstKind.NIL), (P_Root.IArgType_AnonFunDecl__2)localVarStack.LocalVarDecl, stmt, (P_Root.IArgType_AnonFunDecl__4)localVarStack.ContextLocalVarDecl);
            anonAction.Span = stmt.Span;
            anonAction.id = (P_Root.IArgType_AnonFunDecl__5)MkId(entrySpan, exitSpan);
            parseProgram.AnonFunctions.Add(anonAction);
            localVarStack = new LocalVarStack(this);

            foreach (var e in onEventList)
            {
                var action = P_Root.MkDoDecl(state, (P_Root.IArgType_DoDecl__1)e, anonAction);
                action.Span = span;
                action.id = (P_Root.IArgType_DoDecl__3)MkId(span);
                parseProgram.Dos.Add(action);
                if (isTrigAnnotated)
                {
                    foreach (var kv in annots)
                    {
                        var annot = P_Root.MkAnnotation(
                            action,
                            kv.Item1,
                            (P_Root.IArgType_Annotation__2)kv.Item2);
                        annot.Span = crntAnnotSpan;
                        annot.id = (P_Root.IArgType_Annotation__3)MkId(crntAnnotSpan);
                        parseProgram.Annotations.Add(annot);
                    }
                }
            }

            isTrigAnnotated = false;
            onEventList.Clear();
        }

        private void AddDoNamedAction(string name, Span nameSpan, Span span)
        {
            Contract.Assert(onEventList.Count > 0);
            Contract.Assert(!isTrigAnnotated || crntAnnotStack.Count > 0);

            var state = GetCurrentStateDecl(span);
            var actName = MkString(name, nameSpan);
            var annots = isTrigAnnotated ? crntAnnotStack.Pop() : null;
            foreach (var e in onEventList)
            {
                var action = P_Root.MkDoDecl(state, (P_Root.IArgType_DoDecl__1)e, actName);
                action.Span = span;
                action.id = (P_Root.IArgType_DoDecl__3)MkId(span);
                parseProgram.Dos.Add(action);
                if (isTrigAnnotated)
                {
                    foreach (var kv in annots)
                    {
                        var annot = P_Root.MkAnnotation(
                            action,
                            kv.Item1,
                            (P_Root.IArgType_Annotation__2)kv.Item2);
                        annot.Span = crntAnnotSpan;
                        annot.id = (P_Root.IArgType_Annotation__3)MkId(crntAnnotSpan);
                        parseProgram.Annotations.Add(annot);
                    }
                }
            }

            isTrigAnnotated = false;
            onEventList.Clear();
        }

        private string QualifiedNameToString(P_Root.QualifiedName qualifiedName)
        {
            if (qualifiedName == null)
            {
                return "";
            }
            return QualifiedNameToString(qualifiedName.qualifier as P_Root.QualifiedName) + (qualifiedName.name as P_Root.StringCnst).Value;
        }

        private void AddState(string name, bool isStart, Span nameSpan, Span span)
        {
            var state = GetCurrentStateDecl(span);
            state.Span = span;
            state.id = (P_Root.IArgType_StateDecl__5)MkId(span);
            parseProgram.States.Add(state);
            if (groupStack.Count == 0)
            {
                state.name = P_Root.MkQualifiedName(
                    MkString(name, nameSpan),
                    MkUserCnst(P_Root.UserCnstKind.NIL, span));
                state.name.Span = nameSpan;
            }
            else
            {
                state.name = P_Root.MkQualifiedName(MkString(name, nameSpan), groupStack.Peek());
                state.name.Span = nameSpan;
            }
            
            if (isStart)
            {
                var machDecl = GetCurrentMachineDecl(span);
                if (string.IsNullOrEmpty(((P_Root.StringCnst)machDecl.start[0]).Value))
                {
                    machDecl.start = (P_Root.QualifiedName)state.name;
                }
                else
                {
                    var errFlag = new Flag(
                                     SeverityKind.Error,
                                     span,
                                     Constants.BadSyntax.ToString("Too many start states"),
                                     Constants.BadSyntax.Code,
                                     parseSource);
                    parseFailed = true;
                    parseFlags.Add(errFlag);
                }
            }

            var stateName = QualifiedNameToString(state.name as P_Root.QualifiedName);
            if (crntStateNames.Contains(stateName))
            {
                var errFlag = new Flag(
                                     SeverityKind.Error,
                                     span,
                                     Constants.BadSyntax.ToString(string.Format("A state with name {0} already declared", stateName)),
                                     Constants.BadSyntax.Code,
                                     parseSource);
                parseFailed = true;
                parseFlags.Add(errFlag);
            }
            else
            {
                crntStateNames.Add(stateName);
            }
            
            crntState = null;
        }

        private void AddVarDecls(bool hasAnnots, Span annotSpan)
        {
            Contract.Assert(typeExprStack.Count > 0);
            Contract.Assert(crntVarList.Count > 0);
            Contract.Assert(!hasAnnots || crntAnnotStack.Count > 0);
            var typeExpr = (P_Root.IArgType_VarDecl__2)typeExprStack.Pop();
            var annots = hasAnnots ? crntAnnotStack.Pop() : null;
            foreach (var vd in crntVarList)
            {
                vd.type = typeExpr;
                parseProgram.Variables.Add(vd);

                if (hasAnnots)
                {
                    foreach (var kv in annots)
                    {
                        var annot = P_Root.MkAnnotation(
                            vd,
                            kv.Item1,
                            (P_Root.IArgType_Annotation__2)kv.Item2);
                        annot.Span = annotSpan;
                        annot.id = (P_Root.IArgType_Annotation__3)MkId(annotSpan);
                        parseProgram.Annotations.Add(annot);
                    }
                }
            }

            crntVarList.Clear();
        }

        private void AddEvent(string name, Span nameSpan, Span span)
        {
            var evDecl = GetCurrentEventDecl(span);
            evDecl.Span = span;
            evDecl.name = MkString(name, nameSpan);
            parseProgram.Events.Add(evDecl);
            if (IsValidName(TopDecl.Event, name, nameSpan))
            {
                topDeclNames.eventNames.Add(name);
            }
            crntEventDecl = null;
        }

        private void SetMachine(P_Root.UserCnstKind kind, string name, Span nameSpan, Span span)
        {
            var machDecl = GetCurrentMachineDecl(span);
            machDecl.Span = span;
            machDecl.name = MkString(name, nameSpan);
            machDecl.kind = MkUserCnst(kind, span);
            foreach (var e in crntObservesList)
            {
                var observes = P_Root.MkObservesDecl(machDecl, (P_Root.IArgType_ObservesDecl__1)e);
                parseProgram.Observes.Add(observes);
            }
            if (IsValidName(TopDecl.Machine, name, nameSpan))
            {
                topDeclNames.machineNames.Add(name);
            }
        }

        private void AddMachine(Span span)
        {
            var machDecl = GetCurrentMachineDecl(span);
            machDecl.id = (P_Root.IArgType_MachineDecl__4)MkId(span);
            machDecl.Span = span;
            parseProgram.Machines.Add(machDecl);
            crntMachDecl = null;
            crntObservesList.Clear();
            crntStateNames.Clear();
            crntFunNames.Clear();
            crntVarNames.Clear();
            crntEventList.Clear();
        }

        private void AddMachineProto(string name, Span nameSpan, Span span)
        {
            var machProto = GetCurrentMachineProtoDecl(span);
            machProto.name = MkString(name, nameSpan);
            machProto.Span = span;
            parseProgram.MachineProtoDecls.Add(machProto);
            crntMachProtoDecl = null;
            if (IsValidName(TopDecl.MachineProto, name, nameSpan))
            {
                topDeclNames.machineProto.Add(name);
            }
        }

        private void AddReceivesList(Span span)
        {
           
            Contract.Assert(crntEventList.Count > 0);
            foreach(var ev in crntEventList)
            {
                var rec = P_Root.MkMachineReceives(GetCurrentMachineDecl(span), (P_Root.IArgType_MachineReceives__1)ev);
                rec.Span = ev.Span;
                parseProgram.MachineReceives.Add(rec);
            }
            crntEventList.Clear();
           
        }

        private void AddReceivesAll()
        {
            var recAll = P_Root.MkMachineReceivesAllEvents(GetCurrentMachineDecl(Span.Unknown));
            parseProgram.MachineReceivesAllEvents.Add(recAll);
        }
        private void AddSendsList(Span span)
        {
            Contract.Assert(crntEventList.Count > 0);
            foreach (var ev in crntEventList)
            {
                var send = P_Root.MkMachineSends(GetCurrentMachineDecl(span), (P_Root.IArgType_MachineSends__1)ev);
                send.Span = ev.Span;
                parseProgram.MachineSends.Add(send);
                crntEventList.Clear();
            }
            
        }
        
        private void AddSendsAll()
        {
            var sendAll = P_Root.MkMachineSendsAllEvents(GetCurrentMachineDecl(Span.Unknown));
            parseProgram.MachineSendsAllEvents.Add(sendAll);
        }


        private void AddCreatesList(Span span)
        {
            Contract.Assert(crntStringIdList.Count > 0);
            foreach (var id in crntStringIdList)
            {
                var creates = P_Root.MkMachineCreates(GetCurrentMachineDecl(span), (P_Root.IArgType_MachineCreates__1)id);
                creates.Span = id.Span;
                parseProgram.MachineCreates.Add(creates);
            }
            crntStringIdList.Clear();
        }

        private void AddCreatesAll()
        { 
            var createsAll = P_Root.MkMachineCreatesAll(GetCurrentMachineDecl(Span.Unknown));
            parseProgram.MachineCreatesAll.Add(createsAll);
            
        }

        private void AddToCreatesList(string name, Span nameSpan)
        {
            crntStringIdList.Add(MkString(name, nameSpan));
        }

        private void AddExportsInterface(string interfaceName, Span interfaceSpan, Span span)
        {
            var machDecl = GetCurrentMachineDecl(span);
            var export = new P_Root.MachineExports();
            export.iname = (P_Root.IArgType_MachineExports__1)MkString(interfaceName, interfaceSpan);
            export.mach = machDecl;
            export.Span = span;
            parseProgram.MachineExports.Add(export);
        }

       
        private void AddMachineAnnots(Span span)
        {
            Contract.Assert(crntAnnotStack.Count > 0);
            var machDecl = GetCurrentMachineDecl(span);
            var annots = crntAnnotStack.Pop();
            foreach (var kv in annots)
            {
                var annot = P_Root.MkAnnotation(
                    machDecl,
                    kv.Item1,
                    (P_Root.IArgType_Annotation__2)kv.Item2);
                annot.Span = span;
                annot.id = (P_Root.IArgType_Annotation__3)MkId(span);
                parseProgram.Annotations.Add(annot);
            }            
        }

        private void AddStateAnnots(Span span)
        {
            Contract.Assert(crntAnnotStack.Count > 0);
            var stateDecl = GetCurrentStateDecl(span);
            var annots = crntAnnotStack.Pop();
            foreach (var kv in annots)
            {
                var annot = P_Root.MkAnnotation(
                    stateDecl,
                    kv.Item1,
                    (P_Root.IArgType_Annotation__2)kv.Item2);
                annot.Span = span;
                annot.id = (P_Root.IArgType_Annotation__3)MkId(span);
                parseProgram.Annotations.Add(annot);
            }
        }

        private void AddEventAnnots(Span span)
        {
            Contract.Assert(crntAnnotStack.Count > 0);
            var eventDecl = GetCurrentEventDecl(span);
            var annots = crntAnnotStack.Pop();
            foreach (var kv in annots)
            {
                var annot = P_Root.MkAnnotation(
                    eventDecl,
                    kv.Item1,
                    (P_Root.IArgType_Annotation__2)kv.Item2);
                annot.Span = span;
                annot.id = (P_Root.IArgType_Annotation__3)MkId(span);
                parseProgram.Annotations.Add(annot);
            }
        }

        private void AddFunAnnots(Span span)
        {
            Contract.Assert(crntAnnotStack.Count > 0);
            var funDecl = GetCurrentFunDecl(span);
            var annots = crntAnnotStack.Pop();
            foreach (var kv in annots)
            {
                var annot = P_Root.MkAnnotation(
                    funDecl,
                    kv.Item1,
                    (P_Root.IArgType_Annotation__2)kv.Item2);
                annot.Span = span;
                annot.id = (P_Root.IArgType_Annotation__3)MkId(span);
                parseProgram.Annotations.Add(annot);
            }
        }

        private void AddFunction(Span span, Span entrySpan, Span exitSpan)
        {
            Contract.Assert(stmtStack.Count > 0);

            bool isGlobal = crntMachDecl == null;
            var funDecl = GetCurrentFunDecl(span);
            funDecl.Span = span;
            funDecl.owner = isGlobal ? (P_Root.IArgType_FunDecl__1) MkUserCnst(P_Root.UserCnstKind.NIL, span) 
                                     : (P_Root.IArgType_FunDecl__1) GetCurrentMachineDecl(span);
            funDecl.locals = (P_Root.IArgType_FunDecl__5)localVarStack.LocalVarDecl;
            funDecl.body = (P_Root.IArgType_FunDecl__6)stmtStack.Pop();
            funDecl.id = (P_Root.IArgType_FunDecl__7)MkId(entrySpan, exitSpan);
            parseProgram.Functions.Add(funDecl);
            localVarStack = new LocalVarStack(this);
            crntFunDecl = null;
        }

        private void AddFunProto(Span span)
        {
            Contract.Assert(isFunProtoDecl);
            var funProtoDecl = GetCurrentFunProtoDecl(span);
            parseProgram.FunProtoDecls.Add(funProtoDecl);
            crntFunProtoDecl = null;
            isFunProtoDecl = false;
        }
        #endregion

        #region Node getters
        private P_Root.EventDecl GetCurrentEventDecl(Span span)
        {
            if (crntEventDecl != null)
            {
                return crntEventDecl;
            }
            
            crntEventDecl = P_Root.MkEventDecl();
            crntEventDecl.card = MkUserCnst(P_Root.UserCnstKind.NIL, span);
            crntEventDecl.type = MkUserCnst(P_Root.UserCnstKind.NIL, span);
            crntEventDecl.id = (P_Root.IArgType_EventDecl__3)MkId(span);
            crntEventDecl.Span = span;
            return crntEventDecl;
        }

        private P_Root.InterfaceTypeDecl GetCurrentInterfaceTypeDecl(Span span)
        {
            if (crntInterfaceDecl != null)
            {
                return crntInterfaceDecl;
            }

            crntInterfaceDecl = P_Root.MkInterfaceTypeDecl();
            crntInterfaceDecl.Span = span;
            crntInterfaceDecl.argType = (P_Root.IArgType_InterfaceTypeDecl__2)MkBaseType(P_Root.UserCnstKind.NULL, Span.Unknown);
            return crntInterfaceDecl;
        }

        private P_Root.FunDecl GetCurrentFunDecl(Span span)
        {
            if (crntFunDecl != null)
            {
                return crntFunDecl;
            }

            crntFunDecl = P_Root.MkFunDecl();
            crntFunDecl.kind = MkUserCnst(P_Root.UserCnstKind.REAL, span);
            crntFunDecl.@params = MkUserCnst(P_Root.UserCnstKind.NIL, span);
            crntFunDecl.@return = MkUserCnst(P_Root.UserCnstKind.NIL, span);
            crntFunDecl.Span = span;
            return crntFunDecl;
        }

        private P_Root.FunProtoDecl GetCurrentFunProtoDecl(Span span)
        {
            if (crntFunProtoDecl != null)
            {
                return crntFunProtoDecl;
            }

            crntFunProtoDecl = P_Root.MkFunProtoDecl();
            crntFunProtoDecl.@params = MkUserCnst(P_Root.UserCnstKind.NIL, span);
            crntFunProtoDecl.@return = MkUserCnst(P_Root.UserCnstKind.NIL, span);
            crntFunProtoDecl.Span = span;
            return crntFunProtoDecl;
        }

        private P_Root.StateDecl GetCurrentStateDecl(Span span)
        {
            if (crntState != null)
            {
                return crntState;
            }

            crntState = P_Root.MkStateDecl();
            crntState.Span = span;
            crntState.owner = GetCurrentMachineDecl(span);

            crntState.entryAction = MkSkipFun((P_Root.MachineDecl)crntState.owner, span);
            crntState.exitFun = MkSkipFun((P_Root.MachineDecl)crntState.owner, span);
            crntState.temperature = MkUserCnst(P_Root.UserCnstKind.WARM, span);
            return crntState;
        }
        
        private P_Root.MachineDecl GetCurrentMachineDecl(Span span)
        {
            if (crntMachDecl != null)
            {
                return crntMachDecl;
            }

            crntMachDecl = P_Root.MkMachineDecl();
            crntMachDecl.name = MkString(string.Empty, span);
            crntMachDecl.kind = MkUserCnst(P_Root.UserCnstKind.REAL, span);
            crntMachDecl.card = MkUserCnst(P_Root.UserCnstKind.NIL, span);
            crntMachDecl.start = P_Root.MkQualifiedName(
                                        MkString(string.Empty, span),
                                        MkUserCnst(P_Root.UserCnstKind.NIL, span));
            crntMachDecl.start.Span = span;
            return crntMachDecl;
        }

        private P_Root.MachineProtoDecl GetCurrentMachineProtoDecl(Span span)
        {
            if (crntMachProtoDecl != null)
            {
                return crntMachProtoDecl;
            }

            crntMachProtoDecl = P_Root.MkMachineProtoDecl();
            crntMachProtoDecl.constType = (P_Root.IArgType_MachineProtoDecl__1)MkBaseType(P_Root.UserCnstKind.NULL, Span.Unknown);
            return crntMachProtoDecl;
        }
        #endregion

        #region Helpers
        private static bool IsSkipFun(P_Root.GroundTerm term)
        {
            P_Root.NulStmt nulStmt = null;
            if (term is P_Root.AnonFunDecl)
            {
                nulStmt = ((P_Root.AnonFunDecl)term).body as P_Root.NulStmt;
            }

            if (nulStmt == null)
            {
                return false;
            }
            else
            {
                return ((P_Root.UserCnstKind)((P_Root.UserCnst)nulStmt[0]).Value) == P_Root.UserCnstKind.SKIP;
            }
        }

        private int GetNextTrampolineLabel()
        {
            return nextTrampolineLabel++;
        }

        private int GetNextPayloadVarLabel()
        {
            return nextPayloadVarLabel++;
        }

        private P_Root.AnonFunDecl MkSkipFun(P_Root.MachineDecl owner, Span span)
        {
            var stmt = P_Root.MkNulStmt(MkUserCnst(P_Root.UserCnstKind.SKIP, span));
            stmt.id = (P_Root.IArgType_NulStmt__1) MkId(span);
            stmt.Span = span;
            var field = P_Root.MkNmdTupTypeField(
                                   P_Root.MkUserCnst(P_Root.UserCnstKind.NONE),
                                   P_Root.MkString("_payload_skip"),
                                   (P_Root.IArgType_NmdTupTypeField__2)MkBaseType(P_Root.UserCnstKind.NULL, Span.Unknown));
            var decl = P_Root.MkAnonFunDecl(owner, P_Root.MkUserCnst(P_Root.UserCnstKind.NIL), P_Root.MkUserCnst(P_Root.UserCnstKind.NIL), stmt, (P_Root.IArgType_AnonFunDecl__4)P_Root.MkNmdTupType(field, P_Root.MkUserCnst(P_Root.UserCnstKind.NIL)));
            decl.Span = span;
            parseProgram.AnonFunctions.Add(decl);
            return decl;
        }

        private P_Root.TypeExpr MkBaseType(P_Root.UserCnstKind kind, Span span)
        {
            Contract.Requires(
                kind == P_Root.UserCnstKind.NULL ||
                kind == P_Root.UserCnstKind.BOOL ||
                kind == P_Root.UserCnstKind.INT ||
                kind == P_Root.UserCnstKind.MACHINE ||
                kind == P_Root.UserCnstKind.EVENT ||
                kind == P_Root.UserCnstKind.ANY);

            var cnst = P_Root.MkUserCnst(kind);
            cnst.Span = span;
            var bt = P_Root.MkBaseType(cnst);
            bt.Span = span;
            return bt;
        }

        private P_Root.UserCnst MkUserCnst(P_Root.UserCnstKind kind, Span span)
        {
            var cnst = P_Root.MkUserCnst(kind);
            cnst.Span = span;
            return cnst;
        }

        private P_Root.StringCnst MkString(string s, Span span)
        {
            var str = P_Root.MkString(s);
            str.Span = span;
            return str;
        }

        private P_Root.RealCnst MkNumeric(int i, Span span)
        {
            var num = P_Root.MkNumeric(i);
            num.Span = span;
            return num;
        }

        private bool ParseFormatString(string s, int numArgs, Span span, out List<string> segments, out List<int> formatArgs)
        {
            segments = null;
            formatArgs = null;
            var ss = new List<string>();
            var ns = new List<int>();
            int i = 0;
            string curr = "";
            while (i < s.Length)
            {
                if ((s[i] == '{' || s[i] == '}') && i + 1 == s.Length)
                {
                    goto error;
                }
                if (s[i] == '{')
                {
                    i = i + 1;
                    if (s[i] == '{')
                    {
                        curr += '{';
                    }
                    else
                    {
                        int j = i;
                        while (j - i < 3 && j < s.Length && char.IsDigit(s[j]))
                        {
                            j++;
                        }
                        int n;
                        if (i < j && j < s.Length && s[j] == '}' && int.TryParse(s.Substring(i, j-i), out n))
                        {
                            if (n >= numArgs)
                            {
                                goto error;
                            }
                            ss.Add(curr);
                            ns.Add(n);
                            curr = "";
                            i = j;
                        }
                        else
                        {
                            goto error;
                        }
                    }
                }
                else if (s[i] == '}')
                {
                    i = i + 1;
                    if (s[i] == '}')
                    {
                        curr += '}';
                    }
                    else
                    {
                        goto error;
                    }
                }
                else
                {
                    curr += s[i];
                }
                i++;
            }
            ss.Add(curr);
            segments = ss;
            formatArgs = ns;
            Contract.Assert(0 < segments.Count && segments.Count == formatArgs.Count + 1);
            return true;

            error:
            var errFlag = new Flag(
                            SeverityKind.Error,
                            span,
                            Constants.BadSyntax.ToString(string.Format("Bad format string {0}", s)),
                            Constants.BadSyntax.Code,
                            parseSource);
            parseFailed = true;
            parseFlags.Add(errFlag);
            return false;
        }

        private void ResetState()
        {
            stmtStack.Clear();
            valueExprStack.Clear();
            exprsStack.Clear();
            typeExprStack.Clear();
            crntVarList.Clear();
            groupStack.Clear();
            crntEventList.Clear();
            crntAnnotStack.Clear();
            crntAnnotList = new List<Tuple<P_Root.StringCnst, P_Root.AnnotValue>>();
            parseFailed = false;
            isTrigAnnotated = false;
            crntState = null;
            crntEventDecl = null;
            crntMachDecl = null;
            crntInterfaceDecl = null;
            crntStateTargetName = null;
            crntGotoTargetName = null;
            nextPayloadVarLabel = 0;
            crntStateNames.Clear();
            crntFunNames.Clear();
            crntVarNames.Clear();
            topDeclNames.Reset();
            anonEventSetCounter = 0;
        }
        #endregion
    }
}


