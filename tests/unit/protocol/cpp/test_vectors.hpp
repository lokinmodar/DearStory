#pragma once

#include <string>
#include <string_view>

std::string read_vector(std::string_view file_name);
bool json_semantically_equal(std::string_view left, std::string_view right);
