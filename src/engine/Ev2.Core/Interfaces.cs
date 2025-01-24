using Dual.Common.Base.FS;

using static Dual.Common.Core.FS.GraphModule;
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
    public interface IWithGraph : IContainer { }
    public abstract class Vertex : NamedObject, INamedVertex
    {
        public Vertex(string name) : base(name) {}
    }
}


namespace Dual.Ev2
{
    public interface IDsObject {}
    public interface IDsNamedObject : IDsObject, INamed {}
    public interface ISystem : IDsNamedObject, IContainer { }
    public interface IFlow : IDsNamedObject, IContain { }
    public interface IWork : IDsNamedObject, IContain, INamedVertex { }
    public interface ICoin : IDsObject, INamedVertex, IContainee { }
    public interface ICall : ICoin { }

    public partial class System : NamedObject, ISystem
    {
        public System(string name) : base(name) {}

        public List<Flow> Flows { get; set; }

        public IEnumerable<IContainee> Containees => Flows;
    }

    public partial class Flow : NamedObject, IFlow, IWithGraph
    {
        public System System { get; set; }
        public Flow(System system, string name) : base(name) {
            System = system;
        }
        public List<Work> Works { get; set; }
        public List<ICoin> Coins { get; set; }
        public IEnumerable<Call> Calls => Coins.OfType<Call>();
        public IEnumerable<IContainee> Containees => Works.Cast<IContainee>().Concat(Coins);

        public IContainer Container => System;
    }

    public partial class Work : Vertex, IWork, IWithGraph
    {
        public Flow Flow { get; set; } // set for JSON
        public List<ICoin> Coins { get; set; }
        public Work(Flow flow, string name) : base(name)
        {
            Flow = flow;
        }

        public IEnumerable<IContainee> Containees => Coins;
        public IContainer Container => Flow;
    }
    public partial class Call : Coin, ICall
    {
        public Call(IWithGraph parent, string name) : base(parent, name) {}
    }

    public partial class Coin : Vertex, ICoin
    {
        public IWithGraph Parent { get; set; }
        public Coin(IWithGraph parent, string name) : base(name)
        {
            Parent = parent;
        }

        public IContainer Container => Parent;
    }
}



namespace Dual.Ev2
{
    public partial class System
    {
        public bool CreateFlow(string flowName)
        {
            if (Flows.Exists(f => f.Name == flowName))
                return false;
            var f = new Flow(this, flowName);
            Flows.Add(f);
            return true;
        }
    }

    public partial class Flow
    {
        public bool CreateWork(string workName)
        {
            if (Works.Exists(w => w.Name == workName))
                return false;
            var w = new Work(this, workName);
            Works.Add(w);
            return true;
        }
    }

    public partial class Work
    {
        public bool CreateCall(string callName)
        {
            if (Coins.Exists(c => c.Name == callName))
                return false;
            Coin c = new Call(this, callName);
            Coins.Add(c);
            return true;
        }
    }


}