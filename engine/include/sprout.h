#ifndef SPROUT_H
#define SPROUT_H

#include <stddef.h>
#include <stdint.h>

#ifdef __cplusplus
extern "C" {
#endif

#define SPROUT_API_VERSION 1u
#define SPROUT_OPERATION_CHECK 0u
#define SPROUT_OPERATION_RUN 1u
#define SPROUT_PROFILE_GUIDED 0u
#define SPROUT_PROFILE_STRUCTURED 1u
#define SPROUT_PROFILE_ADVANCED 2u

typedef struct SproutBuffer {
    uint8_t* data;
    size_t length;
    size_t capacity;
} SproutBuffer;

uint32_t sprout_api_version(void);

SproutBuffer sprout_execute(
    uint32_t operation,
    uint32_t profile,
    const uint8_t* source_name_data,
    size_t source_name_length,
    const uint8_t* source_data,
    size_t source_length);

SproutBuffer sprout_project_load(
    const uint8_t* project_path_data,
    size_t project_path_length);

SproutBuffer sprout_project_create(
    const uint8_t* destination_data,
    size_t destination_length,
    const uint8_t* name_data,
    size_t name_length,
    const uint8_t* template_data,
    size_t template_length);

void sprout_buffer_free(SproutBuffer buffer);

#ifdef __cplusplus
}
#endif

#endif
