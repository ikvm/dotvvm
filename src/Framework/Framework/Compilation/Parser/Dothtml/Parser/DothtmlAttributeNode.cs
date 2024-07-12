using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using DotVVM.Framework.Compilation.Parser.Dothtml.Tokenizer;

namespace DotVVM.Framework.Compilation.Parser.Dothtml.Parser
{
    public sealed class DothtmlAttributeNode : DothtmlNode
    {
        public override string ToString() =>
            AttributeFullName + ValueNode switch {
                null => "",
                DothtmlValueBindingNode => $"={ValueNode}",
                _ => $"=\"{ValueNode}\""
            };
        public string? AttributePrefix => AttributePrefixNode?.Text;

        public string AttributeName => AttributeNameNode.Text;
        public string AttributeFullName => AttributePrefix == null ? AttributeName : AttributePrefix + ":" + AttributeName;

        public DothtmlValueNode? ValueNode { get; set; }

        public DothtmlNameNode? AttributePrefixNode { get; set; }

        public DothtmlNameNode AttributeNameNode { get; set; }

        public DothtmlToken? PrefixSeparatorToken { get; set; }
        public DothtmlToken? ValueSeparatorToken { get; set; }
        public IEnumerable<DothtmlToken>? ValueStartTokens { get; set; }
        public IEnumerable<DothtmlToken>? ValueEndTokens { get; set; }

        public DothtmlAttributeNode(DothtmlNameNode attributeNameNode)
        {
            this.AttributeNameNode = attributeNameNode;
        }

        public override IEnumerable<DothtmlNode> EnumerateChildNodes()
        {
            if (AttributePrefixNode != null)
            {
                yield return AttributePrefixNode;
            }
            yield return AttributeNameNode;
            if (ValueNode != null)
            {
                yield return ValueNode;
            }
        }

        public override void Accept(IDothtmlSyntaxTreeVisitor visitor)
        {
            visitor.Visit(this);

            AttributePrefixNode?.AcceptIfCondition(visitor);
            AttributeNameNode.AcceptIfCondition(visitor);
            ValueNode?.AcceptIfCondition(visitor);
        }

        internal new void AcceptIfCondition(IDothtmlSyntaxTreeVisitor visitor)
        {
            if (visitor.Condition(this))
                this.Accept(visitor);
        }

        public override IEnumerable<DothtmlNode> EnumerateNodes()
        {
            return base.EnumerateNodes().Concat(EnumerateChildNodes().SelectMany(node => node.EnumerateNodes()));
        }
    }
}
