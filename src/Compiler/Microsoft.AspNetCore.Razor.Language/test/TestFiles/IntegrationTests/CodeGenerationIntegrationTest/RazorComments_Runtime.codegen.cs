﻿#pragma checksum "TestFiles/IntegrationTests/CodeGenerationIntegrationTest/RazorComments.cshtml" "{8829d00f-11b8-4213-878b-770e8597ac16}" "a9d16af76d3b638c489fe4ce4511816619e1713e1515ec0ce13b898e140dde7d"
// <auto-generated/>
#pragma warning disable 1591
[assembly: global::Microsoft.AspNetCore.Razor.Hosting.RazorCompiledItemAttribute(typeof(Microsoft.AspNetCore.Razor.Language.IntegrationTests.TestFiles.TestFiles_IntegrationTests_CodeGenerationIntegrationTest_RazorComments_Runtime), @"default", @"/TestFiles/IntegrationTests/CodeGenerationIntegrationTest/RazorComments.cshtml")]
namespace Microsoft.AspNetCore.Razor.Language.IntegrationTests.TestFiles;
#line hidden
[global::Microsoft.AspNetCore.Razor.Hosting.RazorSourceChecksumAttribute(@"SHA256", @"a9d16af76d3b638c489fe4ce4511816619e1713e1515ec0ce13b898e140dde7d", @"/TestFiles/IntegrationTests/CodeGenerationIntegrationTest/RazorComments.cshtml")]
public class TestFiles_IntegrationTests_CodeGenerationIntegrationTest_RazorComments_Runtime
{
    #pragma warning disable 1998
    public async System.Threading.Tasks.Task ExecuteAsync()
    {
        WriteLiteral("<p>This should ");
        WriteLiteral(" be shown</p>\r\n\r\n");
#nullable restore
#line 5 "TestFiles/IntegrationTests/CodeGenerationIntegrationTest/RazorComments.cshtml"
                                       
    Exception foo = 

#line default
#line hidden
#nullable disable
#nullable restore
#line 6 "TestFiles/IntegrationTests/CodeGenerationIntegrationTest/RazorComments.cshtml"
                                                  null;
    if(foo != null) {
        throw foo;
    }

#line default
#line hidden
#nullable disable
        WriteLiteral("\r\n");
#nullable restore
#line 12 "TestFiles/IntegrationTests/CodeGenerationIntegrationTest/RazorComments.cshtml"
   var bar = "@* bar *@"; 

#line default
#line hidden
#nullable disable
        WriteLiteral("<p>But this should show the comment syntax: ");
#nullable restore
#line (13,46)-(13,49) 6 "TestFiles/IntegrationTests/CodeGenerationIntegrationTest/RazorComments.cshtml"
Write(bar);

#line default
#line hidden
#nullable disable
        WriteLiteral("</p>\r\n\r\n");
#nullable restore
#line (15,3)-(15,4) 6 "TestFiles/IntegrationTests/CodeGenerationIntegrationTest/RazorComments.cshtml"
Write(ab);

#line default
#line hidden
#nullable disable
        WriteLiteral("\r\n\r\n<input value=\"@*this razor comment is the actual value*@\" type=\"text\" />\r\n<input ");
        WriteLiteral(" type=\"text\" />\r\n");
    }
    #pragma warning restore 1998
}
#pragma warning restore 1591
