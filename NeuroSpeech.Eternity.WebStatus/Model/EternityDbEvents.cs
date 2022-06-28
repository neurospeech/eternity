using NeuroSpeech.EntityAccessControl;

namespace NeuroSpeech.Eternity.WebStatus.Model
{
    public class EternityDbEvents: DbContextEvents<EternityDbContext>
    {
        public EternityDbEvents()
        {
            Register<BaseEntityEvents<Activity>>();
            Register<BaseEntityEvents<Workflow>>();
        }
    }

    internal class BaseEntityEvents<T>: DbEntityEvents<BaseEntity>
        where T: BaseEntity
    {
        public override IQueryContext<BaseEntity> Filter(IQueryContext<BaseEntity> q)
        {
            return q;
        }

        public override Task InsertingAsync(BaseEntity entity)
        {
            throw new NotSupportedException();
        }

        public override Task UpdatingAsync(BaseEntity entity)
        {
            throw new NotSupportedException();
        }

        public override Task DeletingAsync(BaseEntity entity)
        {
            throw new NotSupportedException();
        }
    }
}
