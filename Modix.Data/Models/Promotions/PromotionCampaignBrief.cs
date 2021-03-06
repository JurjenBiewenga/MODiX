﻿using System;
using System.Linq.Expressions;

using Modix.Data.Models.Core;
using Modix.Data.Projectables;

namespace Modix.Data.Models.Promotions
{
    /// <summary>
    /// Describes a partial view of a <see cref="PromotionCampaignEntity"/>, for use within the context of another projected model.
    /// </summary>
    public class PromotionCampaignBrief
    {
        /// <summary>
        /// See <see cref="PromotionCampaignEntity.Id"/>.
        /// </summary>
        public long Id { get; set; }

        /// <summary>
        /// See <see cref="PromotionCampaignEntity.Subject"/>.
        /// </summary>
        public GuildUserBrief Subject { get; set; }

        /// <summary>
        /// See <see cref="PromotionCampaignEntity.TargetRole"/>.
        /// </summary>
        public GuildRoleBrief TargetRole { get; set; }

        /// <summary>
        /// See <see cref="PromotionCampaignEntity.Outcome"/>.
        /// </summary>
        public PromotionCampaignOutcome? Outcome { get; set; }

        internal static Expression<Func<PromotionCampaignEntity, PromotionCampaignBrief>> FromEntityProjection
            = entity => new PromotionCampaignBrief()
            {
                Id = entity.Id,
                Subject = entity.Subject.Project(GuildUserBrief.FromEntityProjection),
                TargetRole = entity.TargetRole.Project(GuildRoleBrief.FromEntityProjection),
                // https://github.com/aspnet/EntityFrameworkCore/issues/12834
                //Outcome = entity.Outcome,
                Outcome = (entity.Outcome == null)
                    ? null
                    : (PromotionCampaignOutcome?)Enum.Parse<PromotionCampaignOutcome>(entity.Outcome.ToString()),
            };
    }
}
