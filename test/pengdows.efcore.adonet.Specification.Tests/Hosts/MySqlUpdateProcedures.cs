// Behaviour reference (not copied): Pomelo's StoredProcedureUpdateMySqlTest (MIT, Pomelo Foundation):
// https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/blob/14c2cd558e37c71e7a2fca75e339ef902effdf60/test/EFCore.MySql.FunctionalTests/Update/StoredProcedureUpdateMySqlTest.cs

namespace pengdows.efcore.adonet.Specification.Tests;

/// <summary>
///     MySQL's procedures for EF's stored-procedure update suite. Outputs are OUT and INOUT parameters (read back through
///     session variables); a result column is a SELECT at the procedure's end. The row version is a TIMESTAMP(6) the
///     server sets on insert and update, declared by the procedures' SQL. The result-column procedures are specific to this
///     provider's output-result-set contract.
/// </summary>
public static class MySqlUpdateProcedures
{
    private const string EntityInsertWithOutput = """
        CREATE PROCEDURE "Entity_Insert"(pName varchar(255), OUT pId int)
        BEGIN
            INSERT INTO "Entity" ("Name") VALUES (pName);
            SET pId = LAST_INSERT_ID();
        END
        """;

    private const string EntityUpdateWithRowsAffected = """
        CREATE PROCEDURE "Entity_Update"(pId int, pName varchar(255), OUT pRowsAffected int)
        BEGIN
            UPDATE "Entity" SET "Name" = pName WHERE "Id" = pId;
            SET pRowsAffected = ROW_COUNT();
        END
        """;

    private const string EntityUpdateSelectingRowsAffected = """
        CREATE PROCEDURE "Entity_Update"(pId int, pName varchar(255))
        BEGIN
            UPDATE "Entity" SET "Name" = pName WHERE "Id" = pId;
            SELECT ROW_COUNT();
        END
        """;

    private const string EntityInsertWithInputOutputName = """
        CREATE PROCEDURE "Entity_Insert"(OUT pId int, INOUT pName varchar(255))
        BEGIN
            IF pName IS NULL THEN
                INSERT INTO "Entity" ("Name") VALUES ('Some default value');
                SET pName = 'Some default value';
            ELSE
                INSERT INTO "Entity" ("Name") VALUES (pName);
                SET pName = NULL;
            END IF;
            SET pId = LAST_INSERT_ID();
        END
        """;

    // The row version: set by the server on insert and update, which the column's DEFAULT ... ON UPDATE says.
    private const string RowVersion = """
        ALTER TABLE "Entity" MODIFY "ConcurrencyToken" TIMESTAMP(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6)
        GO

        """;

    public static UpdateProcedures Instance { get; } = new(
        new Dictionary<string, string>
        {
            ["Insert_with_output_parameter"] = EntityInsertWithOutput,
            ["Insert_twice_with_output_parameter"] = EntityInsertWithOutput,
            ["Insert_with_result_column"] = """
                CREATE PROCEDURE "Entity_Insert"(pName varchar(255))
                BEGIN
                    INSERT INTO "Entity" ("Name") VALUES (pName);
                    SELECT LAST_INSERT_ID();
                END
                """,
            ["Insert_with_two_result_columns"] = """
                CREATE PROCEDURE "EntityWithAdditionalProperty_Insert"(pName varchar(255))
                BEGIN
                    INSERT INTO "EntityWithAdditionalProperty" ("Name") VALUES (pName);
                    SELECT "AdditionalProperty", "Id" FROM "EntityWithAdditionalProperty" WHERE "Id" = LAST_INSERT_ID();
                END
                """,
            ["Update"] = """
                CREATE PROCEDURE "Entity_Update"(pId int, pName varchar(255))
                UPDATE "Entity" SET "Name" = pName WHERE "Id" = pId
                """,
            ["Update_partial"] = """
                CREATE PROCEDURE "EntityWithAdditionalProperty_Update"(pId int, pName varchar(255), pAdditionalProperty int)
                UPDATE "EntityWithAdditionalProperty" SET "Name" = pName, "AdditionalProperty" = pAdditionalProperty WHERE "Id" = pId
                """,
            ["Delete"] = """
                CREATE PROCEDURE "Entity_Delete"(pId int)
                DELETE FROM "Entity" WHERE "Id" = pId
                """,
            ["Delete_and_insert"] = EntityInsertWithOutput + """

                GO
                CREATE PROCEDURE "Entity_Delete"(pId int)
                DELETE FROM "Entity" WHERE "Id" = pId
                """,
            ["Rows_affected_parameter"] = EntityUpdateWithRowsAffected,
            ["Rows_affected_parameter_and_concurrency_failure"] = EntityUpdateWithRowsAffected,
            ["Rows_affected_result_column"] = EntityUpdateSelectingRowsAffected,
            ["Rows_affected_result_column_and_concurrency_failure"] = EntityUpdateSelectingRowsAffected,
            ["Store_generated_concurrency_token_as_in_out_parameter"] = RowVersion + """
                CREATE PROCEDURE "Entity_Update"(pId int, INOUT pConcurrencyToken timestamp(6), pName varchar(255), OUT pRowsAffected int)
                BEGIN
                    UPDATE "Entity" SET "Name" = pName WHERE "Id" = pId AND "ConcurrencyToken" = pConcurrencyToken;
                    SET pRowsAffected = ROW_COUNT();
                    SELECT "ConcurrencyToken" INTO pConcurrencyToken FROM "Entity" WHERE "Id" = pId;
                END
                """,
            ["Store_generated_concurrency_token_as_two_parameters"] = RowVersion + """
                CREATE PROCEDURE "Entity_Update"(pId int, pConcurrencyTokenIn timestamp(6), pName varchar(255), OUT pConcurrencyTokenOut timestamp(6), OUT pRowsAffected int)
                BEGIN
                    UPDATE "Entity" SET "Name" = pName WHERE "Id" = pId AND "ConcurrencyToken" = pConcurrencyTokenIn;
                    SET pRowsAffected = ROW_COUNT();
                    SELECT "ConcurrencyToken" INTO pConcurrencyTokenOut FROM "Entity" WHERE "Id" = pId;
                END
                """,
            ["User_managed_concurrency_token"] = """
                CREATE PROCEDURE "EntityWithAdditionalProperty_Update"(pId int, pConcurrencyTokenOriginal int, pName varchar(255), pConcurrencyTokenCurrent int, OUT pRowsAffected int)
                BEGIN
                    UPDATE "EntityWithAdditionalProperty" SET "Name" = pName, "AdditionalProperty" = pConcurrencyTokenCurrent
                    WHERE "Id" = pId AND "AdditionalProperty" = pConcurrencyTokenOriginal;
                    SET pRowsAffected = ROW_COUNT();
                END
                """,
            ["Original_and_current_value_on_non_concurrency_token"] = """
                CREATE PROCEDURE "Entity_Update"(pId int, pNameCurrent varchar(255), pNameOriginal varchar(255))
                BEGIN
                    IF pNameCurrent <> pNameOriginal THEN
                        UPDATE "Entity" SET "Name" = pNameCurrent WHERE "Id" = pId;
                    END IF;
                END
                """,
            ["Input_or_output_parameter_with_input"] = EntityInsertWithInputOutputName,
            ["Input_or_output_parameter_with_output"] = EntityInsertWithInputOutputName,
            ["Tpt"] = """
                CREATE PROCEDURE "Parent_Insert"(OUT pId int, pName varchar(255))
                BEGIN
                    INSERT INTO "Parent" ("Name") VALUES (pName);
                    SET pId = LAST_INSERT_ID();
                END
                GO
                CREATE PROCEDURE "Child1_Insert"(pId int, pChild1Property int)
                INSERT INTO "Child1" ("Id", "Child1Property") VALUES (pId, pChild1Property)
                """,
            ["Tpt_mixed_sproc_and_non_sproc"] = """
                CREATE PROCEDURE "Parent_Insert"(OUT pId int, pName varchar(255))
                BEGIN
                    INSERT INTO "Parent" ("Name") VALUES (pName);
                    SET pId = LAST_INSERT_ID();
                END
                """,
            ["Tpc"] = """
                CREATE PROCEDURE "Child1_Insert"(OUT pId int, pName varchar(255), pChild1Property int)
                BEGIN
                    INSERT INTO "Child1" ("Name", "Child1Property") VALUES (pName, pChild1Property);
                    SET pId = LAST_INSERT_ID();
                END
                """,
            ["Non_sproc_followed_by_sproc_commands_in_the_same_batch"] = """
                CREATE PROCEDURE "EntityWithAdditionalProperty_Insert"(pName text, OUT pId int, pAdditional_property int)
                BEGIN
                    INSERT INTO "EntityWithAdditionalProperty" ("Name", "AdditionalProperty") VALUES (pName, pAdditional_property);
                    SET pId = LAST_INSERT_ID();
                END
                """
        },
        (entityTypeBuilder, propertyName) => entityTypeBuilder.Property<DateTime>(propertyName)
            .IsConcurrencyToken()
            .ValueGeneratedOnAddOrUpdate()
            .HasColumnType("TIMESTAMP(6)"));
}
