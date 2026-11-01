// Behaviour reference (not copied): Npgsql's StoredProcedureUpdateNpgsqlTest (PostgreSQL License), the EF Core stored-procedure
// update suite it derives from: https://github.com/npgsql/efcore.pg

namespace pengdows.efcore.adonet.Specification.Tests;

/// <summary>
///     PostgreSQL's procedures for EF's stored-procedure update suite. The core emits <c>CALL</c>, which PostgreSQL 11 and later
///     run on procedures written with <c>CREATE PROCEDURE</c>. Output parameters come back from <c>CALL</c> as the procedure's OUT
///     arguments. A procedure cannot return a result set through <c>CALL</c>, so the result-column cases are not written here: the
///     capability is absent from the dialect, and those tests are skipped with that reason, not with the provider's name. The row
///     version is a timestamp set by a trigger on insert and update, which is how PostgreSQL keeps a server-maintained column.
/// </summary>
public static class PostgreSqlUpdateProcedures
{
    private const string EntityInsertWithOutput = """
        CREATE PROCEDURE "Entity_Insert"(pName varchar(255), OUT pId int)
        LANGUAGE plpgsql AS $$
        BEGIN
            INSERT INTO "Entity" ("Name") VALUES (pName) RETURNING "Id" INTO pId;
        END
        $$
        """;

    private const string EntityUpdateWithRowsAffected = """
        CREATE PROCEDURE "Entity_Update"(pId int, pName varchar(255), OUT pRowsAffected int)
        LANGUAGE plpgsql AS $$
        BEGIN
            UPDATE "Entity" SET "Name" = pName WHERE "Id" = pId;
            GET DIAGNOSTICS pRowsAffected = ROW_COUNT;
        END
        $$
        """;

    private const string EntityInsertWithInputOutputName = """
        CREATE PROCEDURE "Entity_Insert"(OUT pId int, INOUT pName varchar(255))
        LANGUAGE plpgsql AS $$
        BEGIN
            IF pName IS NULL THEN
                INSERT INTO "Entity" ("Name") VALUES ('Some default value') RETURNING "Id" INTO pId;
                pName := 'Some default value';
            ELSE
                INSERT INTO "Entity" ("Name") VALUES (pName) RETURNING "Id" INTO pId;
                pName := NULL;
            END IF;
        END
        $$
        """;

    // The row version: a trigger sets the column on every insert and update, the way MySQL's ON UPDATE clause does.
    private const string RowVersion = """
        CREATE FUNCTION "Entity_RowVersion"() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
            NEW."ConcurrencyToken" := clock_timestamp();
            RETURN NEW;
        END
        $$
        GO
        CREATE TRIGGER "Entity_RowVersion" BEFORE INSERT OR UPDATE ON "Entity"
            FOR EACH ROW EXECUTE FUNCTION "Entity_RowVersion"()

        GO

        """;

    public static UpdateProcedures Instance { get; } = new(
        new Dictionary<string, string>
        {
            ["Insert_with_output_parameter"] = EntityInsertWithOutput,
            ["Insert_twice_with_output_parameter"] = EntityInsertWithOutput,
            ["Update"] = """
                CREATE PROCEDURE "Entity_Update"(pId int, pName varchar(255))
                LANGUAGE sql AS $$
                UPDATE "Entity" SET "Name" = pName WHERE "Id" = pId
                $$
                """,
            ["Update_partial"] = """
                CREATE PROCEDURE "EntityWithAdditionalProperty_Update"(pId int, pName varchar(255), pAdditionalProperty int)
                LANGUAGE sql AS $$
                UPDATE "EntityWithAdditionalProperty" SET "Name" = pName, "AdditionalProperty" = pAdditionalProperty WHERE "Id" = pId
                $$
                """,
            ["Delete"] = """
                CREATE PROCEDURE "Entity_Delete"(pId int)
                LANGUAGE sql AS $$
                DELETE FROM "Entity" WHERE "Id" = pId
                $$
                """,
            ["Delete_and_insert"] = EntityInsertWithOutput + """

                GO
                CREATE PROCEDURE "Entity_Delete"(pId int)
                LANGUAGE sql AS $$
                DELETE FROM "Entity" WHERE "Id" = pId
                $$
                """,
            ["Rows_affected_parameter"] = EntityUpdateWithRowsAffected,
            ["Rows_affected_parameter_and_concurrency_failure"] = EntityUpdateWithRowsAffected,
            ["Store_generated_concurrency_token_as_in_out_parameter"] = RowVersion + """
                CREATE PROCEDURE "Entity_Update"(pId int, INOUT pConcurrencyToken timestamp(6), pName varchar(255), OUT pRowsAffected int)
                LANGUAGE plpgsql AS $$
                BEGIN
                    UPDATE "Entity" SET "Name" = pName WHERE "Id" = pId AND "ConcurrencyToken" = pConcurrencyToken;
                    GET DIAGNOSTICS pRowsAffected = ROW_COUNT;
                    SELECT "ConcurrencyToken" INTO pConcurrencyToken FROM "Entity" WHERE "Id" = pId;
                END
                $$
                """,
            ["Store_generated_concurrency_token_as_two_parameters"] = RowVersion + """
                CREATE PROCEDURE "Entity_Update"(pId int, pConcurrencyTokenIn timestamp(6), pName varchar(255), OUT pConcurrencyTokenOut timestamp(6), OUT pRowsAffected int)
                LANGUAGE plpgsql AS $$
                BEGIN
                    UPDATE "Entity" SET "Name" = pName WHERE "Id" = pId AND "ConcurrencyToken" = pConcurrencyTokenIn;
                    GET DIAGNOSTICS pRowsAffected = ROW_COUNT;
                    SELECT "ConcurrencyToken" INTO pConcurrencyTokenOut FROM "Entity" WHERE "Id" = pId;
                END
                $$
                """,
            ["User_managed_concurrency_token"] = """
                CREATE PROCEDURE "EntityWithAdditionalProperty_Update"(pId int, pConcurrencyTokenOriginal int, pName varchar(255), pConcurrencyTokenCurrent int, OUT pRowsAffected int)
                LANGUAGE plpgsql AS $$
                BEGIN
                    UPDATE "EntityWithAdditionalProperty" SET "Name" = pName, "AdditionalProperty" = pConcurrencyTokenCurrent
                    WHERE "Id" = pId AND "AdditionalProperty" = pConcurrencyTokenOriginal;
                    GET DIAGNOSTICS pRowsAffected = ROW_COUNT;
                END
                $$
                """,
            ["Original_and_current_value_on_non_concurrency_token"] = """
                CREATE PROCEDURE "Entity_Update"(pId int, pNameCurrent varchar(255), pNameOriginal varchar(255))
                LANGUAGE plpgsql AS $$
                BEGIN
                    IF pNameCurrent <> pNameOriginal THEN
                        UPDATE "Entity" SET "Name" = pNameCurrent WHERE "Id" = pId;
                    END IF;
                END
                $$
                """,
            ["Input_or_output_parameter_with_input"] = EntityInsertWithInputOutputName,
            ["Input_or_output_parameter_with_output"] = EntityInsertWithInputOutputName,
            ["Tpt"] = """
                CREATE PROCEDURE "Parent_Insert"(OUT pId int, pName varchar(255))
                LANGUAGE plpgsql AS $$
                BEGIN
                    INSERT INTO "Parent" ("Name") VALUES (pName) RETURNING "Id" INTO pId;
                END
                $$
                GO
                CREATE PROCEDURE "Child1_Insert"(pId int, pChild1Property int)
                LANGUAGE sql AS $$
                INSERT INTO "Child1" ("Id", "Child1Property") VALUES (pId, pChild1Property)
                $$
                """,
            ["Tpt_mixed_sproc_and_non_sproc"] = """
                CREATE PROCEDURE "Parent_Insert"(OUT pId int, pName varchar(255))
                LANGUAGE plpgsql AS $$
                BEGIN
                    INSERT INTO "Parent" ("Name") VALUES (pName) RETURNING "Id" INTO pId;
                END
                $$
                """,
            ["Tpc"] = """
                CREATE PROCEDURE "Child1_Insert"(OUT pId int, pName varchar(255), pChild1Property int)
                LANGUAGE plpgsql AS $$
                BEGIN
                    INSERT INTO "Child1" ("Name", "Child1Property") VALUES (pName, pChild1Property) RETURNING "Id" INTO pId;
                END
                $$
                """,
            ["Non_sproc_followed_by_sproc_commands_in_the_same_batch"] = """
                CREATE PROCEDURE "EntityWithAdditionalProperty_Insert"(pName text, OUT pId int, pAdditional_property int)
                LANGUAGE plpgsql AS $$
                BEGIN
                    INSERT INTO "EntityWithAdditionalProperty" ("Name", "AdditionalProperty") VALUES (pName, pAdditional_property) RETURNING "Id" INTO pId;
                END
                $$
                """
        },
        (entityTypeBuilder, propertyName) => entityTypeBuilder.Property<DateTime>(propertyName)
            .IsConcurrencyToken()
            .ValueGeneratedOnAddOrUpdate()
            .HasColumnType("timestamp(6)"));
}
