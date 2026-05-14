using System.Xml;
using System.Xml.Schema;
using AdeptTools.Workflow.Models;

namespace AdeptTools.Workflow.Input;

public class WorkflowXmlReader
{
    public WorkflowExcelInput Read(string filePath)
    {
        var result = new WorkflowExcelInput();

        var settings = new XmlReaderSettings();
        var schemaStream = typeof(WorkflowXmlReader).Assembly
            .GetManifestResourceStream("AdeptTools.Workflow.Schema.AdeptWorkflowConfig.xsd");

        if (schemaStream is not null)
        {
            settings.Schemas.Add(null, XmlReader.Create(schemaStream));
            settings.ValidationType = ValidationType.Schema;
            var validationErrors = new List<string>();
            settings.ValidationEventHandler += (_, e) => validationErrors.Add(e.Message);
        }

        var doc = new XmlDocument();
        using (var reader = XmlReader.Create(filePath, settings))
        {
            doc.Load(reader);
        }

        var root = doc.DocumentElement;
        if (root is null)
            return result;

        result.ServerUrl = GetChildText(root, "ServerUrl");
        result.ProjectName = GetChildText(root, "ProjectName");
        var dryRunText = GetChildText(root, "DryRun");
        if (bool.TryParse(dryRunText, out var dryRun))
            result.DryRun = dryRun;

        var workflowsNode = root.SelectSingleNode("Workflows");
        if (workflowsNode is null)
            return result;

        foreach (XmlNode wfNode in workflowsNode.SelectNodes("Workflow")!)
        {
            if (wfNode is not XmlElement wfElement) continue;

            var model = new WorkflowInputModel
            {
                Name = wfElement.GetAttribute("Name"),
                Active = ParseBoolAttr(wfElement, "Active", true),
                Memo = GetChildText(wfElement, "Memo"),
                ExcludeSaturday = ParseBoolAttr(wfElement, "ExcludeSaturday", false),
                ExcludeSunday = ParseBoolAttr(wfElement, "ExcludeSunday", false)
            };

            var timeoutText = GetChildText(wfElement, "TimeoutDays");
            if (int.TryParse(timeoutText, out var timeout))
                model.TimeoutDays = timeout;

            var recurringText = GetChildText(wfElement, "RecurringTimeoutDays");
            if (int.TryParse(recurringText, out var recurring))
                model.RecurringTimeoutDays = recurring;

            var stepsNode = wfElement.SelectSingleNode("Steps");
            if (stepsNode is not null)
            {
                foreach (XmlNode stepNode in stepsNode.SelectNodes("Step")!)
                {
                    if (stepNode is not XmlElement stepElement) continue;

                    var step = new WorkflowInputStep
                    {
                        Name = stepElement.GetAttribute("Name"),
                        AutoAdvance = ParseBoolAttr(stepElement, "AutoAdvance", false)
                    };

                    var approvalsAttr = stepElement.GetAttribute("ApprovalsRequired");
                    if (int.TryParse(approvalsAttr, out var approvals))
                        step.RequiredApprovalsCount = approvals;

                    var trusteesNode = stepElement.SelectSingleNode("Trustees");
                    if (trusteesNode is not null)
                    {
                        foreach (XmlNode trusteeNode in trusteesNode.SelectNodes("Trustee")!)
                        {
                            if (trusteeNode is not XmlElement trusteeElement) continue;

                            var id = trusteeElement.GetAttribute("Id");
                            var typeText = trusteeElement.GetAttribute("Type");
                            var roleText = trusteeElement.GetAttribute("Role");

                            if (!string.IsNullOrWhiteSpace(id) && TrusteeTypeMapper.TryMap(typeText, out var trusteeType))
                            {
                                TrusteeTypeMapper.TryMapRole(roleText, out var role);

                                step.Trustees.Add(new WorkflowInputTrustee
                                {
                                    TrusteeId = id,
                                    TrusteeType = trusteeType,
                                    Role = role
                                });
                            }
                        }
                    }

                    model.Steps.Add(step);
                }
            }

            result.Workflows.Add(model);
        }

        return result;
    }

    private static string GetChildText(XmlElement parent, string childName)
    {
        var node = parent.SelectSingleNode(childName);
        return node?.InnerText?.Trim() ?? string.Empty;
    }

    private static bool ParseBoolAttr(XmlElement element, string attrName, bool defaultValue)
    {
        var value = element.GetAttribute(attrName);
        if (string.IsNullOrEmpty(value)) return defaultValue;
        return bool.TryParse(value, out var result) ? result : defaultValue;
    }
}
