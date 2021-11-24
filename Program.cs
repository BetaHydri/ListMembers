using CommandLineParameters;
using System;
using System.Collections.Generic;
using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using System.DirectoryServices.ActiveDirectory;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace ListMembers
{
    internal class Program
    {
        public static IEnumerable<string> GetGroupMemberList(DirectoryEntry group, bool recursive = false, Dictionary<string, string> domainSidMapping = null)
        {
            var members = new List<string>();

            group.RefreshCache(new[] { "member", "canonicalName" });

            if (domainSidMapping == null)
            {
                //Find all the trusted domains and create a dictionary that maps the domain's SID to its DNS name
                var groupCn = (string)group.Properties["canonicalName"].Value;
                var domainDns = groupCn.Substring(0, groupCn.IndexOf("/", StringComparison.Ordinal));

                var domain = Domain.GetDomain(new DirectoryContext(DirectoryContextType.Domain, domainDns));
                var trusts = domain.GetAllTrustRelationships();

                domainSidMapping = new Dictionary<string, string>();

                foreach (TrustRelationshipInformation trust in trusts)
                {
                    using (var trustedDomain = new DirectoryEntry($"LDAP://{trust.TargetName}")) // UserName and Password possible to extend
                    {
                        try
                        {
                            trustedDomain.RefreshCache(new[] { "objectSid" });
                            var domainSid = new SecurityIdentifier((byte[])trustedDomain.Properties["objectSid"].Value, 0).ToString();
                            domainSidMapping.Add(domainSid, trust.TargetName);
                        }
                        catch (Exception e)
                        {
                            //This can happen if you're running this with credentials
                            //that aren't trusted on the other domain or if the domain
                            //can't be contacted
                            throw new Exception($"Can't connect to domain {trust.TargetName}: {e.Message}", e);
                        }
                    }
                }
            }

            var membersFound = 0;
            while (true)
            {
                var memberDns = group.Properties["member"];
                foreach (string member in memberDns)
                {
                    using (var memberDe = new DirectoryEntry($"LDAP://{member.Replace("/", "\\/")}"))
                    {
                        memberDe.RefreshCache(new[] { "objectClass", "msDS-PrincipalName", "cn", "distinguishedName" });

                        if (recursive && memberDe.Properties["objectClass"].Contains("group"))
                        {
                            members.AddRange(GetGroupMemberList(memberDe, true, domainSidMapping));
                        }
                        else if (memberDe.Properties["objectClass"].Contains("foreignSecurityPrincipal"))
                        {
                            //User is on a trusted domain
                            var foreignUserSid = memberDe.Properties["cn"].Value.ToString();
                            //The SID of the domain is the SID of the user minus the last block of numbers
                            var foreignDomainSid = foreignUserSid.Substring(0, foreignUserSid.LastIndexOf("-"));
                            if (domainSidMapping.TryGetValue(foreignDomainSid, out var foreignDomainDns))
                            {
                                using (var foreignMember = new DirectoryEntry($"LDAP://{foreignDomainDns}/<SID={foreignUserSid}>"))
                                {
                                    foreignMember.RefreshCache(new[] { "msDS-PrincipalName", "objectClass", "distinguishedName" });
                                    if (recursive && foreignMember.Properties["objectClass"].Contains("group"))
                                    {
                                        members.AddRange(GetGroupMemberList(foreignMember, true, domainSidMapping));
                                    }
                                    else
                                    {
                                        members.Add(foreignMember.Properties["distinguishedName"].Value.ToString());
                                    }
                                }
                            }
                            else
                            {
                                //unknown domain
                                members.Add(foreignUserSid);
                            }
                        }
                        else
                        {
                            try
                            {
                                string username = memberDe.Properties["distinguishedName"].Value.ToString();
                                //var username = memberDe.Properties["msDS-PrincipalName"].Value.ToString();
                                if (!string.IsNullOrEmpty(username))
                                {
                                    members.Add(username);
                                }
                            }
                            catch
                            {
                                /*foreach (string propName in memberDe.Properties.PropertyNames)
                                {
                                   Console.WriteLine("Property: " + propName);
                                }
                                string username = memberDe.Properties["distinguishedName"].Value.ToString();
                                if (!string.IsNullOrEmpty(username))
                                {
                                    members.Add(username);
                                }*/
                            }
                        }
                    }
                }

                if (memberDns.Count == 0) break;
                membersFound += memberDns.Count;
                try
                {
                    group.RefreshCache(new[] { $"member;range={membersFound}-*" });
                }
                catch (COMException e)
                {
                    if (e.ErrorCode == unchecked((int)0x80072020))
                    { //no more results
                        break;
                    }
                    throw;
                }
            }
            return members;
        }
        private static DirectoryEntry GetGroupDistinguishedName(string GroupName)
        {
            try
            {
                var domain = new PrincipalContext(ContextType.Domain);
                var groupDN = GroupPrincipal.FindByIdentity(domain, GroupName);
                return new DirectoryEntry("LDAP://" + groupDN.DistinguishedName);
            }
            catch
            {
                return null;
            }

        }
        static void Main(string[] args)
        {
            int Result;
            string Errors;

            GetCommandLineParameters Parameters = new GetCommandLineParameters();
            Result = Parameters.ReadCommandLineParameters(args, out Errors);
            if (Result == 10)
            {
                Console.WriteLine("");
                Console.WriteLine("ListMembers");
                Console.WriteLine("");
                Console.WriteLine("This tool enumerates the Groupmembership for a given group in the actual Domain and trying to resolve also the FSPs");
                Console.WriteLine("");
                Console.WriteLine("Usage: ListMembers /GroupName:testgroup /Recursive:false");
                Console.WriteLine("or");
                Console.WriteLine("Usage: ListMembers /GroupName:testgroup /Recursive:true");
                Console.WriteLine("");
                Console.WriteLine("Output: distinguishedName");
                Console.WriteLine("");
                Console.WriteLine("C# Code: from Jan Tiedemann");
                return;
            }

            //Console.WriteLine(Parameters.PrintParametersText());

            if (Result == 5)
            {
                Console.WriteLine("");
                Console.WriteLine("CommandLineParameters");
                Console.WriteLine("");
                Console.WriteLine("A parameter has been specified that is not known");
                return;
            }

            if (Result != 0)
            {
                Console.WriteLine("");
                Console.WriteLine("CommandLineParameters");
                Console.WriteLine("");
                Console.WriteLine("There is a syntax error or conversion error in the command line parameter");
                return;
            }

            //check if admin & passwort where supplied
            if (Parameters.GroupName == null)
            {
                Console.WriteLine("Parameters GroupName must be supplied.");
                return;
            }

            string GroupName = Parameters.GroupName;
            bool Recursive = Parameters.Recursive;
            DirectoryEntry group = GetGroupDistinguishedName(GroupName);
            if (group != null)
            {
                var memberlist = GetGroupMemberList(group, Recursive);
                if (memberlist != null)
                {
                    foreach (var member in memberlist)
                    {
                        Console.WriteLine(member);
                    }
                }
                else
                {
                    Console.WriteLine("Members in group " + GroupName + "is: " + memberlist.Count());
                }
            }
            else
            {
                Console.WriteLine("Group: " + GroupName + " not found!");
            }
        }
    }
}
