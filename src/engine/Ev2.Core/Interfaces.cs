using Dual.Common.Base.FS;

using System.Text.Json.Serialization;

using static Engine.Common.GraphModule;


namespace Dual.Ev2
{
    /// <summary>
    /// 내부에 IContainee 형식의 다른 요소를 포함할 수 있는 parent role 수행
    /// </summary>
    public interface IContainer {
        IEnumerable<IContainee> Containees { get; }
    }

    /// <summary>
    /// IContainer 에 포함될 수 있는 요소의 interface.  child role 수행
    /// </summary>
    public interface IContainee {
        IContainer Container { get; }
    }
    public interface IContain : IContainer, IContainee {}
    /// <summary>
    /// IFlow or IWork
    /// </summary>
    public interface IWithGraph : IContainer {}

    public abstract class DsNamedObject : INamed
    {
        public string Name { get; set; }
        protected DsNamedObject(string name)
        {
            Name = name;
        }
    }
    public abstract class Vertex : DsNamedObject, INamedVertex
    {
        public Vertex(string name) : base(name) {}
    }
}


namespace Dual.Ev2
{
    public interface IDsObject {}
    public interface IDsDsNamedObject : IDsObject, INamed {}
    public interface ISystem : IDsDsNamedObject, IContainer {}
    public interface IFlow : IDsDsNamedObject, IContain {}
    public interface IWork : IDsDsNamedObject, IContain, INamedVertex {}
    public interface ICoin : IDsObject, INamedVertex, IContainee {}
    public interface ICall : ICoin {}

    public partial class DsSystem : DsNamedObject, ISystem
    {
        internal DsSystem(string name) : base(name) {}

        public List<DsFlow> Flows { get; set; } = new();

        [JsonIgnore] public IEnumerable<IContainee> Containees => Flows;
    }

    public partial class DsFlow : DsNamedObject, IFlow, IWithGraph
    {
        [JsonIgnore] public DsSystem System { get; set; }
        internal DsFlow(DsSystem system, string name) : base(name) {
            System = system;
        }
        public List<DsWork> Works { get; set; } = new();
        public List<ICoin> Coins { get; set; } = new();
        [JsonIgnore] public IEnumerable<DsCall> Calls => Coins.OfType<DsCall>();
        [JsonIgnore] public IEnumerable<IContainee> Containees => Works.Cast<IContainee>().Concat(Coins);

        [JsonIgnore] public IContainer Container => System;
    }

    public partial class DsWork : Vertex, IWork, IWithGraph
    {
        [JsonIgnore] public DsFlow Flow { get; set; } // set for JSON
        public List<ICoin> Coins { get; set; } = new();
        internal DsWork(DsFlow flow, string name) : base(name)
        {
            Flow = flow;
        }

        [JsonIgnore] public IEnumerable<IContainee> Containees => Coins;
        [JsonIgnore] public IContainer Container => Flow;
    }
    public partial class DsCall : DsCoin, ICall
    {
        internal DsCall(IWithGraph parent, string name) : base(parent, name) {}
    }

    public partial class DsCoin : Vertex, ICoin
    {
        [JsonIgnore] public IWithGraph Parent { get; set; }
        internal DsCoin(IWithGraph parent, string name) : base(name)
        {
            Parent = parent;
        }

        [JsonIgnore] public IContainer Container => Parent;
    }
}



namespace Dual.Ev2
{
    public partial class DsSystem
    {
        public static DsSystem Create(string name) => new DsSystem(name);
        public DsFlow CreateFlow(string flowName)
        {
            if (Flows.Exists(f => ((INamed)f).Name == flowName))
                return null;
            return new DsFlow(this, flowName).Tee(Flows.Add);
        }
    }

    public partial class DsFlow
    {
        public DsWork CreateWork(string workName)
        {
            if (Works.Exists(w => ((INamed)w).Name == workName))
                return null;
            return new DsWork(this, workName).Tee(Works.Add);
        }
    }

    public partial class DsWork
    {
        public DsCall CreateCall(string callName)
        {
            if (Coins.Exists(c => c.Name == callName))
                return null;
            return new DsCall(this, callName).Tee(Coins.Add);
        }
    }
}
