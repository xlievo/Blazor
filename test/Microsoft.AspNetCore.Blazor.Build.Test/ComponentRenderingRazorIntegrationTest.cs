// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using Microsoft.AspNetCore.Blazor.RenderTree;
using Microsoft.AspNetCore.Blazor.Test.Helpers;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Microsoft.AspNetCore.Blazor.Build.Test
{
    public class ComponentRenderingRazorIntegrationTest : RazorIntegrationTestBase
    {
        internal override bool UseTwoPhaseCompilation => true;

        [Fact]
        public void Render_ChildComponent_Simple()
        {
            // Arrange
            AdditionalSyntaxTrees.Add(Parse(@"
using Microsoft.AspNetCore.Blazor.Components;

namespace Test
{
    public class MyComponent : BlazorComponent
    {
    }
}
"));

            var component = CompileToComponent(@"
@addTagHelper *, TestAssembly
<MyComponent/>");

            // Act
            var frames = GetRenderTree(component);

            // Assert
            Assert.Collection(
                frames,
                frame => AssertFrame.Component(frame, "Test.MyComponent", 1, 0));
        }

        [Fact]
        public void Render_ChildComponent_WithParameters()
        {
            // Arrange
            AdditionalSyntaxTrees.Add(Parse(@"
using Microsoft.AspNetCore.Blazor.Components;

namespace Test
{
    public class SomeType
    {
    }

    public class MyComponent : BlazorComponent
    {
        [Parameter] int IntProperty { get; set; }
        [Parameter] bool BoolProperty { get; set; }
        [Parameter] string StringProperty { get; set; }
        [Parameter] SomeType ObjectProperty { get; set; }
    }
}
"));

            var component = CompileToComponent(@"
@addTagHelper *, TestAssembly
<MyComponent 
    IntProperty=""123""
    BoolProperty=""true""
    StringProperty=""My string""
    ObjectProperty=""new SomeType()"" />");

            // Act
            var frames = GetRenderTree(component);

            // Assert
            Assert.Collection(
                frames,
                frame => AssertFrame.Component(frame, "Test.MyComponent", 5, 0),
                frame => AssertFrame.Attribute(frame, "IntProperty", 123, 1),
                frame => AssertFrame.Attribute(frame, "BoolProperty", true, 2),
                frame => AssertFrame.Attribute(frame, "StringProperty", "My string", 3),
                frame =>
                {
                    AssertFrame.Attribute(frame, "ObjectProperty", 4);
                    Assert.Equal("Test.SomeType", frame.AttributeValue.GetType().FullName);
                });
        }

        [Fact]
        public void Render_ChildComponent_TriesToSetNonParamter()
        {
            // Arrange
            AdditionalSyntaxTrees.Add(Parse(@"
using Microsoft.AspNetCore.Blazor.Components;

namespace Test
{
    public class MyComponent : BlazorComponent
    {
        public int IntProperty { get; set; }
    }
}
"));

            var component = CompileToComponent(@"
@addTagHelper *, TestAssembly
<MyComponent  IntProperty=""123"" />");

            // Act
            var ex = Assert.Throws<InvalidOperationException>(() => GetRenderTree(component));

            // Assert
            Assert.Equal(
                "Object of type 'Test.MyComponent' has a property matching the name 'IntProperty', " +
                    "but it does not have [ParameterAttribute] applied.",
                ex.Message);
        }

        [Fact]
        public void Render_ChildComponent_WithExplicitStringParameter()
        {
            // Arrange
            AdditionalSyntaxTrees.Add(Parse(@"
using Microsoft.AspNetCore.Blazor.Components;

namespace Test
{
    public class MyComponent : BlazorComponent
    {
        [Parameter]
        string StringProperty { get; set; }
    }
}
"));

            var component = CompileToComponent(@"
@addTagHelper *, TestAssembly
<MyComponent StringProperty=""@(42.ToString())"" />");

            // Act
            var frames = GetRenderTree(component);

            // Assert
            Assert.Collection(
                frames,
                frame => AssertFrame.Component(frame, "Test.MyComponent", 2, 0),
                frame => AssertFrame.Attribute(frame, "StringProperty", "42", 1));
        }

        [Fact]
        public void Render_ChildComponent_WithNonPropertyAttributes()
        {
            // Arrange
            AdditionalSyntaxTrees.Add(Parse(@"
using Microsoft.AspNetCore.Blazor.Components;

namespace Test
{
    public class MyComponent : BlazorComponent, IComponent
    {
        void IComponent.SetParameters(ParameterCollection parameters)
        {
        }
    }
}
"));

            var component = CompileToComponent(@"
@addTagHelper *, TestAssembly
<MyComponent some-attribute=""foo"" another-attribute=""@(42.ToString())"" />");

            // Act
            var frames = GetRenderTree(component);

            // Assert
            Assert.Collection(
                frames,
                frame => AssertFrame.Component(frame, "Test.MyComponent", 3, 0),
                frame => AssertFrame.Attribute(frame, "some-attribute", "foo", 1),
                frame => AssertFrame.Attribute(frame, "another-attribute", "42", 2));
        }


        [Theory]
        [InlineData("e => Increment(e)")]
        [InlineData("(e) => Increment(e)")]
        [InlineData("@(e => Increment(e))")]
        [InlineData("@(e => { Increment(e); })")]
        [InlineData("Increment")]
        [InlineData("@Increment")]
        [InlineData("@(Increment)")]
        public void Render_ChildComponent_WithEventHandler(string expression)
        {
            // Arrange
            AdditionalSyntaxTrees.Add(Parse(@"
using System;
using Microsoft.AspNetCore.Blazor;
using Microsoft.AspNetCore.Blazor.Components;

namespace Test
{
    public class MyComponent : BlazorComponent
    {
        [Parameter]
        Action<UIMouseEventArgs> OnClick { get; set; }
    }
}
"));

            var component = CompileToComponent($@"
@addTagHelper *, TestAssembly
<MyComponent OnClick=""{expression}""/>

@functions {{
    private int counter;
    private void Increment(UIMouseEventArgs e) {{
        counter++;
    }}
}}");

            // Act
            var frames = GetRenderTree(component);

            // Assert
            Assert.Collection(
                frames,
                frame => AssertFrame.Component(frame, "Test.MyComponent", 2, 0),
                frame =>
                {
                    AssertFrame.Attribute(frame, "OnClick", 1);

                    // The handler will have been assigned to a lambda
                    var handler = Assert.IsType<Action<UIMouseEventArgs>>(frame.AttributeValue);
                    Assert.Equal("Test.TestComponent", handler.Target.GetType().FullName);
                });
        }

        [Fact]
        public void Render_ChildComponent_WithExplicitEventHandler()
        {
            // Arrange
            AdditionalSyntaxTrees.Add(Parse(@"
using System;
using Microsoft.AspNetCore.Blazor;
using Microsoft.AspNetCore.Blazor.Components;

namespace Test
{
    public class MyComponent : BlazorComponent
    {
        [Parameter]
        Action<UIEventArgs> OnClick { get; set; }
    }
}
"));

            var component = CompileToComponent(@"
@addTagHelper *, TestAssembly
<MyComponent OnClick=""@Increment""/>

@functions {
    private int counter;
    private void Increment(UIEventArgs e) {
        counter++;
    }
}");

            // Act
            var frames = GetRenderTree(component);

            // Assert
            Assert.Collection(
                frames,
                frame => AssertFrame.Component(frame, "Test.MyComponent", 2, 0),
                frame =>
                {
                    AssertFrame.Attribute(frame, "OnClick", 1);

                    // The handler will have been assigned to a lambda
                    var handler = Assert.IsType<Action<UIEventArgs>>(frame.AttributeValue);
                    Assert.Equal("Test.TestComponent", handler.Target.GetType().FullName);
                    Assert.Equal("Increment", handler.Method.Name);
                });
        }

        [Fact]
        public void Render_ChildComponent_WithMinimizedBoolAttribute()
        {
            // Arrange
            AdditionalSyntaxTrees.Add(Parse(@"
using Microsoft.AspNetCore.Blazor.Components;

namespace Test
{
    public class MyComponent : BlazorComponent
    {
        [Parameter]
        bool BoolProperty { get; set; }
    }
}"));

            var component = CompileToComponent(@"
@addTagHelper *, TestAssembly
<MyComponent BoolProperty />");

            // Act
            var frames = GetRenderTree(component);

            // Assert
            Assert.Collection(
                frames,
                frame => AssertFrame.Component(frame, "Test.MyComponent", 2, 0),
                frame => AssertFrame.Attribute(frame, "BoolProperty", true, 1));
        }

        [Fact]
        public void Render_ChildComponent_WithChildContent()
        {
            // Arrange
            AdditionalSyntaxTrees.Add(Parse(@"
using Microsoft.AspNetCore.Blazor;
using Microsoft.AspNetCore.Blazor.Components;
namespace Test
{
    public class MyComponent : BlazorComponent
    {
        [Parameter]
        string MyAttr { get; set; }

        [Parameter]
        RenderFragment ChildContent { get; set; }
    }
}
"));

            var component = CompileToComponent(@"
@addTagHelper *, TestAssembly
<MyComponent MyAttr=""abc"">Some text<some-child a='1'>Nested text @(""Hello"")</some-child></MyComponent>");

            // Act
            var frames = GetRenderTree(component);

            // Assert: component frames are correct
            Assert.Collection(
                frames,
                frame => AssertFrame.Component(frame, "Test.MyComponent", 3, 0),
                frame => AssertFrame.Attribute(frame, "MyAttr", "abc", 1),
                frame => AssertFrame.Attribute(frame, RenderTreeBuilder.ChildContent, 2));

            // Assert: Captured ChildContent frames are correct
            var childFrames = GetFrames((RenderFragment)frames[2].AttributeValue);
            Assert.Collection(
                childFrames,
                frame => AssertFrame.Text(frame, "Some text", 3),
                frame => AssertFrame.Element(frame, "some-child", 4, 4),
                frame => AssertFrame.Attribute(frame, "a", "1", 5),
                frame => AssertFrame.Text(frame, "Nested text ", 6),
                frame => AssertFrame.Text(frame, "Hello", 7));
        }

        [Fact]
        public void Render_ChildComponent_Nested()
        {
            // Arrange
            AdditionalSyntaxTrees.Add(Parse(@"
using Microsoft.AspNetCore.Blazor;
using Microsoft.AspNetCore.Blazor.Components;

namespace Test
{
    public class MyComponent : BlazorComponent
    {
        [Parameter]
        RenderFragment ChildContent { get; set; }
    }
}
"));

            var component = CompileToComponent(@"
@addTagHelper *, TestAssembly
<MyComponent><MyComponent>Some text</MyComponent></MyComponent>");

            // Act
            var frames = GetRenderTree(component);

            // Assert: outer component frames are correct
            Assert.Collection(
                frames,
                frame => AssertFrame.Component(frame, "Test.MyComponent", 2, 0),
                frame => AssertFrame.Attribute(frame, RenderTreeBuilder.ChildContent, 1));

            // Assert: first level of ChildContent is correct
            // Note that we don't really need the sequence numbers to continue on from the
            // sequence numbers at the parent level. All that really matters is that they are
            // correct relative to each other (i.e., incrementing) within the nesting level.
            // As an implementation detail, it happens that they do follow on from the parent
            // level, but we could change that part of the implementation if we wanted.
            var innerFrames = GetFrames((RenderFragment)frames[1].AttributeValue).ToArray();
            Assert.Collection(
                innerFrames,
                frame => AssertFrame.Component(frame, "Test.MyComponent", 2, 2),
                frame => AssertFrame.Attribute(frame, RenderTreeBuilder.ChildContent, 3));

            // Assert: second level of ChildContent is correct
            Assert.Collection(
                GetFrames((RenderFragment)innerFrames[1].AttributeValue),
                frame => AssertFrame.Text(frame, "Some text", 4));
        }

        [Fact] // https://github.com/aspnet/Blazor/issues/773
        public void Regression_773()
        {
            // Arrange
            AdditionalSyntaxTrees.Add(Parse(@"
using Microsoft.AspNetCore.Blazor.Components;

namespace Test
{
    public class SurveyPrompt : BlazorComponent
    {
        [Parameter] private string Title { get; set; }
    }
}
"));

            var component = CompileToComponent(@"
@addTagHelper *, TestAssembly
@page ""/""

<SurveyPrompt Title=""<div>Test!</div>"" />
");

            // Act
            var frames = GetRenderTree(component);

            // Assert
            Assert.Collection(
                frames,
                frame => AssertFrame.Component(frame, "Test.SurveyPrompt", 2, 0),
                frame => AssertFrame.Attribute(frame, "Title", "<div>Test!</div>", 1));
        }


        [Fact]
        public void Regression_784()
        {
            // Arrange

            // Act
            var component = CompileToComponent(@"
<p onmouseover=""@OnComponentHover"" style=""background: @ParentBgColor;"" />
@functions {
    public string ParentBgColor { get; set; } = ""#FFFFFF"";

    public void OnComponentHover(UIMouseEventArgs e)
    {
    }
}
");

            // Act
            var frames = GetRenderTree(component);

            // Assert
            Assert.Collection(
                frames,
                frame => AssertFrame.Element(frame, "p", 3, 0),
                frame => AssertFrame.Attribute(frame, "onmouseover", 1),
                frame => AssertFrame.Attribute(frame, "style", "background: #FFFFFF;", 2));
        }
    }
}
