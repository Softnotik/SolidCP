// Copyright (c) 2016, SolidCP
// SolidCP is distributed under the Creative Commons Share-alike license
// 
// SolidCP is a fork of WebsitePanel:
// Copyright (c) 2015, Outercurve Foundation.
// All rights reserved.
//
// Redistribution and use in source and binary forms, with or without modification,
// are permitted provided that the following conditions are met:
//
// - Redistributions of source code must  retain  the  above copyright notice, this
//   list of conditions and the following disclaimer.
//
// - Redistributions in binary form  must  reproduce the  above  copyright  notice,
//   this list of conditions  and  the  following  disclaimer in  the documentation
//   and/or other materials provided with the distribution.
//
// - Neither  the  name  of  the  Outercurve Foundation  nor   the   names  of  its
//   contributors may be used to endorse or  promote  products  derived  from  this
//   software without specific prior written permission.
//
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
// ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING,  BUT  NOT  LIMITED TO, THE IMPLIED
// WARRANTIES  OF  MERCHANTABILITY   AND  FITNESS  FOR  A  PARTICULAR  PURPOSE  ARE
// DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE FOR
// ANY DIRECT, INDIRECT, INCIDENTAL,  SPECIAL,  EXEMPLARY, OR CONSEQUENTIAL DAMAGES
// (INCLUDING, BUT NOT LIMITED TO,  PROCUREMENT  OF  SUBSTITUTE  GOODS OR SERVICES;
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION)  HOWEVER  CAUSED AND ON
// ANY  THEORY  OF  LIABILITY,  WHETHER  IN  CONTRACT,  STRICT  LIABILITY,  OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE)  ARISING  IN  ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

using System;
using System.Web.UI.WebControls;
using SolidCP.EnterpriseServer;
using SolidCP.EnterpriseServer.Base.HostedSolution;
using SolidCP.Providers.HostedSolution;

namespace SolidCP.Portal.ExchangeServer
{
    public partial class ExchangeDomainNames : SolidCPModuleBase
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                BindStats();

                // bind domain names
                BindDomainNames();
            }

            
        }

        private void BindStats()
        {
            // set quotas
            OrganizationStatistics stats = ES.Services.Organizations.GetOrganizationStatisticsByOrganization(PanelRequest.ItemID);
            domainsQuota.QuotaUsedValue = stats.CreatedDomains;
            domainsQuota.QuotaValue = stats.AllocatedDomains;
            if (stats.AllocatedDomains != -1) domainsQuota.QuotaAvailable = stats.AllocatedDomains - stats.CreatedDomains;
        }

        public string GetDomainRecordsEditUrl(string domainId)
        {
            return EditUrl("SpaceID", PanelSecurity.PackageId.ToString(), "domain_records",
                    "DomainID=" + domainId,
                    "ItemID=" + PanelRequest.ItemID);
        }

        public bool CheckDomainUsedByHostedOrganization(string domainId)
        {
            int id;
            if (!int.TryParse(domainId, out id)) return false;

            return ES.Services.Organizations.CheckDomainUsedByHostedOrganization(PanelRequest.ItemID, id);
        }

        private void BindDomainNames()
        {
            OrganizationDomainName[] list = ES.Services.Organizations.GetOrganizationDomains(PanelRequest.ItemID);

            gvDomains.DataSource = list;
            gvDomains.DataBind();

            //check if organization has only one default domain
            if (gvDomains.Rows.Count == 1)
            {
                btnSetDefaultDomain.Enabled = false;
            }
        }

        public string IsChecked(bool val)
        {
            return val ? "checked" : "";
        }

        protected void btnAddDomain_Click(object sender, EventArgs e)
        {
            btnSetDefaultDomain.Enabled = true;
            Response.Redirect(EditUrl("ItemID", PanelRequest.ItemID.ToString(), "add_domain",
                "SpaceID=" + PanelSecurity.PackageId));
        }

        protected void gvDomains_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            if (e.CommandName == "DeleteItem")
            {
                // delete domain
                int domainId = Utils.ParseInt(e.CommandArgument.ToString(), 0);

                // domain name
                DomainInfo domain = ES.Services.Servers.GetDomain(domainId);
                var DomainName = domain.DomainName;

                try
                {
                    int result = ES.Services.Organizations.DeleteOrganizationDomain(PanelRequest.ItemID, domainId);
                    if (result < 0)
                    {
                        Response.Redirect(EditUrl("ItemID", PanelRequest.ItemID.ToString(), "check_domain",
                            "SpaceID=" + PanelSecurity.PackageId, "DomainID=" + domainId));
                        return;
                    }
                    //Delete Domain to Mail Cleaner
                    Knom.Helpers.Net.APIMailCleanerHelper.DomainRemove(DomainName);

                    // rebind domains
                    BindDomainNames();

                    BindStats();
                }
                catch (Exception ex)
                {
                    ShowErrorMessage("EXCHANGE_DELETE_DOMAIN", ex);
                }
            }
            else if (e.CommandName == "Change")
            {
                string[] commandArgument = e.CommandArgument.ToString().Split('|');
                int domainId = Utils.ParseInt(commandArgument[0].ToString(), 0);
                ExchangeAcceptedDomainType acceptedDomainType = (ExchangeAcceptedDomainType)Enum.Parse(typeof(ExchangeAcceptedDomainType), commandArgument[1]);


                try
                {

                    ExchangeAcceptedDomainType newDomainType = ExchangeAcceptedDomainType.Authoritative;
                    if (acceptedDomainType == ExchangeAcceptedDomainType.Authoritative)
                        newDomainType = ExchangeAcceptedDomainType.InternalRelay;

                    int result = ES.Services.Organizations.ChangeOrganizationDomainType(PanelRequest.ItemID, domainId, newDomainType);
                    if (result < 0)
                    {
                        messageBox.ShowResultMessage(result);
                        return;
                    }

                    // rebind domains
                    BindDomainNames();

                    BindStats();
                }
                catch (Exception ex)
                {
                    ShowErrorMessage("EXCHANGE_CHANGE_DOMAIN", ex);
                }
            }
            if (e.CommandName == "ViewUsage")
            {
                int domainId = Utils.ParseInt(e.CommandArgument.ToString(), 0);
                Response.Redirect(EditUrl("ItemID", PanelRequest.ItemID.ToString(), "check_domain",
                    "SpaceID=" + PanelSecurity.PackageId, "DomainID=" + domainId));
                return;
            }

        }

        protected void btnSetDefaultDomain_Click(object sender, EventArgs e)
        {
            // get domain
            int domainId = Utils.ParseInt(Request.Form["DefaultDomain"], 0);

            try
            {
                int result = ES.Services.Organizations.SetOrganizationDefaultDomain(PanelRequest.ItemID, domainId);
                if (result < 0)
                {
                    messageBox.ShowResultMessage(result);
                    if (BusinessErrorCodes.ERROR_USER_ACCOUNT_DEMO == result)
                        BindDomainNames();
                    return;
                }

                // rebind domains
                BindDomainNames();
            }
            catch (Exception ex)
            {
                ShowErrorMessage("EXCHANGE_SET_DEFAULT_DOMAIN", ex);
            }
        }

    }
}
