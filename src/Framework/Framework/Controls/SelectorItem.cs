﻿using DotVVM.Framework.Binding;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DotVVM.Framework.Runtime;
using DotVVM.Framework.Hosting;

namespace DotVVM.Framework.Controls
{
    [ControlMarkupOptions(AllowContent = false)]
    public class SelectorItem : HtmlGenericControl
    {
        public string? Text
        {
            get { return (string?)GetValue(TextProperty); }
            set { SetValue(TextProperty, value); }
        }

        public static readonly DotvvmProperty TextProperty =
            DotvvmProperty.Register<string?, SelectorItem>(t => t.Text, null);

        public object? Value
        {
            get { return (object?)GetValue(ValueProperty); }
            set { SetValue(ValueProperty, value); }
        }

        public static readonly DotvvmProperty ValueProperty =
            DotvvmProperty.Register<object?, SelectorItem>(t => t.Value, null);

        public SelectorItem()
            : base("option", false)
        {
        }

        public SelectorItem(string text, object value)
            : this()
        {
            Text = text;
            Value = value;
        }

        public SelectorItem(ValueOrBinding<string> text, ValueOrBinding<object> value)
            : this()
        {
            this.SetValue(TextProperty, text);
            this.SetValue(ValueProperty, value);
        }

        protected override void AddAttributesToRender(IHtmlWriter writer, IDotvvmRequestContext context)
        {
            var value = this.GetValueOrBinding<object>(ValueProperty).EvaluateResourceBinding(this);
            if (value.ValueOrDefault is string s)
            {
                writer.AddAttribute("value", s);
            }
            else
            {
                // anything else than string is better to pass as knockout value binding to avoid issues with `false != 'false'`, ...
                writer.AddKnockoutDataBind("value", value.GetJsExpression(this));
            }
            base.AddAttributesToRender(writer, context);
        }

        protected override void RenderContents(IHtmlWriter writer, IDotvvmRequestContext context)
        {
            if (Text is string t)
                writer.WriteText(t);
            base.RenderContents(writer, context);
        }
    }
}
