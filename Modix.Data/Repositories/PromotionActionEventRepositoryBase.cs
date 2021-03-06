﻿using System.Collections.Generic;
using System.Threading.Tasks;

using Modix.Data.Models.Promotions;

namespace Modix.Data.Repositories
{
    /// <summary>
    /// Describes a repository that creates <see cref="PromotionActionEntity"/> records in the datastore,
    /// and thus consumes <see cref="IPromotionActionEventHandler"/> objects.
    /// </summary>
    public abstract class PromotionActionEventRepositoryBase : RepositoryBase
    {
        /// <summary>
        /// Constructs a new <see cref="PromotionActionEventRepositoryBase"/> object, with the given injected dependencies.
        /// See <see cref="RepositoryBase(ModixContext)"/>.
        /// </summary>
        public PromotionActionEventRepositoryBase(ModixContext modixContext, IEnumerable<IPromotionActionEventHandler> promotionActionEventHandlers)
            : base(modixContext)
        {
            PromotionActionEventHandlers = promotionActionEventHandlers;
        }

        /// <summary>
        /// A set of <see cref="IPromotionActionEventHandler"/> objects to receive information about promotion actions
        /// affected by this repository.
        /// </summary>
        internal protected IEnumerable<IPromotionActionEventHandler> PromotionActionEventHandlers { get; }

        /// <summary>
        /// Notifies <see cref="PromotionActionEventHandlers"/> that a new <see cref="PromotionActionEntity"/> has been created.
        /// </summary>
        /// <param name="promotionAction">The <see cref="PromotionActionEntity"/> that was created.</param>
        /// <returns>A <see cref="Task"/> that will complete when the operation has completed.</returns>
        internal protected async Task RaisePromotionActionCreatedAsync(PromotionActionEntity promotionAction)
        {
            foreach (var handler in PromotionActionEventHandlers)
                await handler.OnPromotionActionCreatedAsync(promotionAction.Id, new PromotionActionCreationData()
                {
                    GuildId = (ulong)promotionAction.GuildId,
                    Type = promotionAction.Type,
                    Created = promotionAction.Created,
                    CreatedById = (ulong)promotionAction.CreatedById 
                });
        }
    }
}
