﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using MountainWarehouse.EasyMWS.Helpers;
using MountainWarehouse.EasyMWS.Model;

namespace MountainWarehouse.EasyMWS.Model
{
    /// <summary>
    /// This is meant to be used as optional argument for MWS report request generation methods from ReportRequestFactoryFba, in order to provide them with groups of marketplaces for which a report should
    /// The MWS endpoint of a marketplaces group is set to the value of the one found on the Marketplace used to initialize the group, and cannot be changed afterward.<para /></summary>
    public sealed class MwsMarketplaceGroup
    {
        private readonly List<MwsMarketplace> _mwsMarketplaces;

        /// <summary>
        /// The MWS access endpoint identifying this group. This value is set when initializing the object with the MWS endpoint of the marketplace provided to the ctor.
        /// </summary>
        public readonly MwsEndpoint MwsEndpoint;

        /// <summary>
        /// Initializes a new marketplace group, and sets the MWS endpoint of this group to the MWS endpoint of the Marketplace provided as parameter.
        /// </summary>
        public MwsMarketplaceGroup(MwsMarketplace marketplace)
        {
            MwsEndpoint = marketplace.MwsEndpoint ?? throw new InvalidOperationException(
                                 $"Cannot initialize marketplace group because the MWS access endpoint is not set on for the provided marketplace");
            _mwsMarketplaces = new List<MwsMarketplace> { marketplace };
        }

        /// <summary>
        /// The readonly list of marketplaces that belong to this group. To add more marketplaces to this group please use the TryAddMarketplace method.
        /// </summary>
        public IEnumerable<string> GetMarketplacesIdList => _mwsMarketplaces.Select(m => m.Id);

        public IEnumerable<MwsMarketplace> GetMarketplaces => _mwsMarketplaces.AsEnumerable();

        /// <summary>
        /// The readonly list of marketplace country codes that belong to this group. To add more marketplaces to this group please use the TryAddMarketplace method.
        /// </summary>
        public IEnumerable<string> GetMarketplacesCountryCodes => _mwsMarketplaces.Select(m => m.CountryCode);

        /// <summary>
        /// Tries to add a marketplace to the current group. <para />
        /// When attempting to add a marketplace to a group, please note that a group can only contain marketplaces with the same MWS endpoint as the one setup for the group at initialization.<para />
        /// Failure in following this convention will result in an InvalidOperationException.<para /> 
        /// Example: A group initialized with an European marketplace cannot also contain a North-American or Asian marketplace.<para/>
        /// If a report has to be requested for marketplaces belonging to different MWS endpoints, then a request object has to be generated for each different MWS endpoint.
        /// </summary>
        /// <param name="marketplace"></param>
        public void TryAddMarketplace(MwsMarketplace marketplace)
        {
            if (marketplace.MwsEndpoint != MwsEndpoint)
            {
                throw new InvalidOperationException(
                    $@"Cannot add marketplace:'{marketplace.Name}' to group with MwsEndpoint:'{MwsEndpoint}',
Because it belongs to a different MwsEndpoint:'{marketplace.MwsEndpoint}'.
In order to request reports for marketplaces belonging to different MwsEndpoints 
you must perform a separate request for each different MwsEndpoint");
            }
            if (GetMarketplacesIdList.Contains(marketplace.Id))
            {
                throw new InvalidOperationException(
                    $@"Cannot add marketplace:'{marketplace.Name}' to group with MwsEndpoint:'{MwsEndpoint}',
Because it the group already contains this marketplace. A marketplace cannot be added twice to the same group");
            }

            _mwsMarketplaces.Add(marketplace);
        }

        /// <summary>
        /// Marketplaces group that contains all amazon marketplaces supported by amazon MWS.<para />
        /// Contains the following marketplaces : Canada, US, Mexico, Spain, UK, France, Germany, Italy, Brazil, India, China, Japan, Australia.
        /// </summary>
        /// <returns></returns>
        public static List<MwsMarketplace> AmazonGlobal()
            => new List<MwsMarketplace>
            {
                MwsMarketplace.Canada ,MwsMarketplace.US , MwsMarketplace.Mexico ,
                MwsMarketplace.Spain ,MwsMarketplace.UK , MwsMarketplace.France , MwsMarketplace.Germany , MwsMarketplace.Italy ,
                MwsMarketplace.Brazil , MwsMarketplace.India , MwsMarketplace.China , MwsMarketplace.Japan , MwsMarketplace.Australia
            };


        /// <summary>
        /// Marketplaces group that contains amazon marketplaces from North America.<para />
        /// Contains the following marketplaces : Canada, US, Mexico.
        /// </summary>
        /// <returns></returns>
        public static List<MwsMarketplace> AmazonNorthAmerica()
            => new List<MwsMarketplace>
            {
                MwsMarketplace.Canada , MwsMarketplace.US , MwsMarketplace.Mexico
            };

        /// <summary>
        /// Marketplaces group that contains amazon marketplaces from Europe.<para />
        /// Contains the following marketplaces : Spain, UK, France, Germany, Italy.
        /// </summary>
        /// <returns></returns>
        public static List<MwsMarketplace> AmazonEurope()
            => new List<MwsMarketplace>
            {
                MwsMarketplace.Spain , MwsMarketplace.UK , MwsMarketplace.France , MwsMarketplace.Germany, MwsMarketplace.Italy
            };

        /// <summary>
        /// Marketplaces group that contains amazon marketplaces that don't belong to the North American or European regions.<para />
        /// Contains the following marketplaces : Brazil, India, China, Japan, Australia.
        /// </summary>
        /// <returns></returns>
        public static List<MwsMarketplace> AmazonOther()
            => new List<MwsMarketplace>
            {
                MwsMarketplace.Brazil, MwsMarketplace.India, MwsMarketplace.China, MwsMarketplace.Japan, MwsMarketplace.Australia
            };

        public static List<MwsMarketplace> AmazonNorthAmericaAndEurope() => AmazonNorthAmerica().AddMarkeplaceGroup(AmazonEurope());

        public static List<MwsMarketplace> AmazonUSAndIndiaAndJapan()
            => new List<MwsMarketplace>
            {
                MwsMarketplace.US, MwsMarketplace.India, MwsMarketplace.Japan
            };
    }

	/// <summary>
	/// Extension methods for the ReportMarketPlacesGroupExtensions class
	/// </summary>
	public static class MwsMarketplaceGroupExtensions
	{
		/// <summary>
		/// Tries to add a marketplace to the current group. <para />
		/// When attempting to add a marketplace to a group, please note that a group can only contain marketplaces with the same MWS endpoint as the one setup for the group at initialization.<para />
		/// Failure in following this convention will result in an InvalidOperationException.<para /> 
		/// Example: A group initialized with an European marketplace cannot also contain a North-American or Asian marketplace.<para/>
		/// If a report has to be requested for marketplaces belonging to different MWS endpoints, then a request object has to be generated for each different MWS endpoint.
		/// </summary>
		/// <returns>The same ReportRequestedMarketplacesGroup object, also containing the newly added marketplace.</returns>
		public static MwsMarketplaceGroup AddMarketplace(this MwsMarketplaceGroup group, MwsMarketplace marketplace)
		{
			group.TryAddMarketplace(marketplace);
			return group;
		}

        /// <summary>
        /// Adds a MwsMarketplace collection to an existing MwsMarketplace collection. Several predefined MwsMarketplace collections are available as static members of MwsMarketplaceGroup.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="targetGroup"></param>
        /// <returns></returns>
        public static List<MwsMarketplace> AddMarkeplaceGroup(this List<MwsMarketplace> source, List<MwsMarketplace> targetGroup)
        {
            if (targetGroup?.Any() != true) return source;
            if (source == null) source = new List<MwsMarketplace>();
            source.AddRange(targetGroup);
            return source;
        }
    }
}
